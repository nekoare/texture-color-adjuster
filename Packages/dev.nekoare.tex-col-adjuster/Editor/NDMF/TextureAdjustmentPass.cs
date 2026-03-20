using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using TexColAdjuster.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TexColAdjuster.Editor.NDMF
{
    public class TextureAdjustmentPass : Pass<TextureAdjustmentPass>
    {
        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<TextureColorAdjustmentComponent>(true);

            foreach (var component in components)
            {
                if (!component.applyDuringBuild)
                    continue;

                ProcessComponent(context, component);
            }
        }

        private void ProcessComponent(BuildContext context, TextureColorAdjustmentComponent component)
        {
            if (component.referenceTexture == null)
            {
                Debug.LogWarning($"[TexColAdjuster] Skipping component on '{component.gameObject.name}': Missing referenceTexture");
                return;
            }

            var bindings = component.EnumerateValidBindings().ToList();
            if (bindings.Count == 0)
            {
                Debug.LogWarning($"[TexColAdjuster] Skipping component on '{component.gameObject.name}': No valid renderer bindings");
                return;
            }

            var processedTextures = new Dictionary<Texture2D, Texture2D>();
            var processedMaterials = new Dictionary<Material, Material>();
            var rendererMaterialsCache = new Dictionary<Renderer, Material[]>();
            int updatedBindings = 0;

            foreach (var binding in bindings)
            {
                var renderer = binding.renderer;
                if (renderer == null)
                {
                    Debug.LogWarning($"[TexColAdjuster] Binding on '{component.gameObject.name}' references a missing renderer. Skipping.");
                    continue;
                }

                var sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials == null)
                {
                    Debug.LogWarning($"[TexColAdjuster] Renderer '{renderer.name}' has no shared materials. Skipping binding.");
                    continue;
                }

                int slot = binding.materialSlot;
                if (slot < 0 || slot >= sharedMaterials.Length)
                {
                    Debug.LogWarning($"[TexColAdjuster] Material slot {slot} is out of range for renderer '{renderer.name}'. Skipping binding.");
                    continue;
                }

                var materials = GetOrCloneMaterials(renderer, rendererMaterialsCache);
                var targetMaterial = materials[slot];
                if (targetMaterial == null)
                {
                    Debug.LogWarning($"[TexColAdjuster] Material slot {slot} on renderer '{renderer.name}' is null. Skipping binding.");
                    continue;
                }

                var originalTexture = targetMaterial.GetTexture("_MainTex") as Texture2D;
                if (originalTexture == null)
                {
                    Debug.LogWarning($"[TexColAdjuster] Renderer '{renderer.name}' material '{targetMaterial.name}' has no _MainTex. Skipping binding.");
                    continue;
                }

                if (!processedTextures.TryGetValue(originalTexture, out var adjustedTexture))
                {
                    adjustedTexture = ProcessTexture(context, component, originalTexture);

                    if (adjustedTexture == null)
                    {
                        Debug.LogError($"[TexColAdjuster] Failed to process texture '{originalTexture.name}' for component '{component.gameObject.name}'");
                        continue;
                    }

                    processedTextures.Add(originalTexture, adjustedTexture);
                }

                if (!processedMaterials.TryGetValue(targetMaterial, out var clonedMaterial))
                {
                    clonedMaterial = Object.Instantiate(targetMaterial);
                    ReplaceTexturesInMaterial(clonedMaterial, processedTextures);

                    var materialAssetName = $"{targetMaterial.name}_TexColAdjusted_{renderer.gameObject.name}";
                    RegisterBakedAsset(context, clonedMaterial, materialAssetName);

                    processedMaterials.Add(targetMaterial, clonedMaterial);
                }
                else
                {
                    ReplaceTexturesInMaterial(clonedMaterial, processedTextures);
                }

                materials[slot] = clonedMaterial;
                renderer.sharedMaterials = materials;
                rendererMaterialsCache[renderer] = materials;
                updatedBindings++;
            }

            if (updatedBindings > 0)
            {
                Debug.Log($"[TexColAdjuster] Processed texture adjustments for {updatedBindings} binding(s) on '{component.gameObject.name}'.");
            }
        }

        private Texture2D ProcessTexture(BuildContext context, TextureColorAdjustmentComponent component, Texture2D originalTexture)
        {
            Texture2D readableTexture = null;
            Texture2D readableReference = null;
            Texture2D result = null;
            TextureImportBackup targetBackup = null;
            TextureImportBackup refBackup = null;

            try
            {
                // Temporarily make textures uncompressed to avoid block noise
                var uncompressedTarget = TextureProcessor.MakeTextureReadable(originalTexture, out targetBackup);
                var uncompressedRef = TextureProcessor.MakeTextureReadable(component.referenceTexture, out refBackup);

                readableTexture = TextureProcessor.MakeReadableCopy(uncompressedTarget ?? originalTexture);
                if (readableTexture == null)
                {
                    targetBackup?.RestoreSettings();
                    refBackup?.RestoreSettings();
                    Debug.LogError($"[TexColorAdjuster] Failed to create readable copy of texture: {originalTexture?.name ?? "null"}");
                    return null;
                }

                readableReference = TextureProcessor.MakeReadableCopy(uncompressedRef ?? component.referenceTexture);
                if (readableReference == null)
                {
                    Debug.LogError($"[TexColorAdjuster] Failed to create readable copy of reference texture: {component.referenceTexture?.name ?? "null"}");
                    return null;
                }

                if (component.useHighPrecisionMode)
                {
                    var highPrecisionConfig = new HighPrecisionProcessor.HighPrecisionConfig
                    {
                        referenceGameObject = component.highPrecisionReferenceObject,
                        materialIndex = component.highPrecisionMaterialIndex,
                        uvChannel = component.highPrecisionUVChannel,
                        dominantColorCount = component.highPrecisionDominantColorCount,
                        useWeightedSampling = component.highPrecisionUseWeightedSampling,
                        maskTexture = component.highPrecisionMaskTexture,
                        maskThreshold = component.highPrecisionMaskThreshold
                    };

                    if (!HighPrecisionProcessor.ValidateHighPrecisionConfig(highPrecisionConfig, readableReference))
                    {
                        Debug.LogWarning($"[TexColorAdjuster] High precision mode is enabled but configuration is invalid. Falling back to standard processing.");
                        result = ColorAdjuster.AdjustColors(
                            readableTexture,
                            readableReference,
                            component.intensity,
                            component.preserveLuminance,
                            component.adjustmentMode
                        );
                    }
                    else
                    {
                        result = HighPrecisionProcessor.ProcessWithHighPrecision(
                            readableTexture,
                            readableReference,
                            highPrecisionConfig,
                            component.intensity * 100f,
                            component.preserveLuminance,
                            component.adjustmentMode
                        );
                    }
                }
                else
                {
                    // Step 1: LAB histogram matching (全体の色合わせ)
                    result = ColorAdjuster.AdjustColors(
                        readableTexture,
                        readableReference,
                        component.intensity,
                        component.preserveLuminance,
                        component.adjustmentMode
                    );

                    // Step 2: DualSelection refinement (選択色域の追加補正)
                    if (component.useDualColorSelection && result != null)
                    {
                        var refined = ColorAdjuster.ApplyDualSelectionRefinement(
                            result, component.targetColor, component.referenceColor, component.selectionRange);
                        if (refined != null)
                        {
                            TextureColorSpaceUtility.UnregisterRuntimeTexture(result);
                            Object.DestroyImmediate(result);
                            result = refined;
                        }
                    }
                }

                // Apply HSBG post-adjustments for all modes
                if (result != null)
                {
                    ApplyPostAdjustments(component, result);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TexColorAdjuster] Exception while processing texture: {ex.Message}\n{ex.StackTrace}");
                result = null;
            }
            finally
            {
                // Clean up temporary texture copies
                if (readableTexture != null && readableTexture != originalTexture)
                {
                    UnityEngine.Object.DestroyImmediate(readableTexture);
                }

                if (readableReference != null && readableReference != component.referenceTexture)
                {
                    UnityEngine.Object.DestroyImmediate(readableReference);
                }

                // Restore original import settings
                targetBackup?.RestoreSettings();
                refBackup?.RestoreSettings();
            }

            if (result != null)
            {
                // Enable MipStreaming BEFORE compression (GetPixels requires uncompressed format for Reinitialize)
                EnableMipStreaming(result);

                // Re-compress to match original format to minimize block artifacts
                CompressToMatch(result, originalTexture.format);

                // Re-apply MipStreaming after compression (CompressTexture may reset it)
                SetMipStreamingFlag(result);

                var textureAssetName = $"{originalTexture.name}_TexColAdjusted_{component.gameObject.name}";
                RegisterBakedAsset(context, result, textureAssetName);
            }

            return result;
        }

        private static void CompressToMatch(Texture2D texture, TextureFormat sourceFormat)
        {
            TextureFormat? compressedFormat = sourceFormat switch
            {
                TextureFormat.DXT1 or TextureFormat.DXT1Crunched => TextureFormat.DXT1,
                TextureFormat.DXT5 or TextureFormat.DXT5Crunched => TextureFormat.DXT5,
                TextureFormat.BC5 => TextureFormat.BC5,
                TextureFormat.BC7 => TextureFormat.BC7,
                TextureFormat.BC4 => TextureFormat.BC4,
                TextureFormat.BC6H => TextureFormat.BC6H,
                _ => null
            };

            if (compressedFormat.HasValue)
            {
                EditorUtility.CompressTexture(texture, compressedFormat.Value, UnityEditor.TextureCompressionQuality.Normal);
            }
        }

        private static void EnableMipStreaming(Texture2D texture)
        {
            if (texture == null) return;

            // MipMapが無いテクスチャにStreamingMipmapsを設定しても効かないため、
            // MipMap付きで再生成する
            if (texture.mipmapCount <= 1)
            {
                var pixels = texture.GetPixels();
                bool linear = !TextureColorSpaceUtility.IsTextureSRGB(texture);
                texture.Reinitialize(texture.width, texture.height, texture.format, true);
                texture.SetPixels(pixels);
                texture.Apply(true); // updateMipmaps = true
            }

            using var serializedTexture = new UnityEditor.SerializedObject(texture);
            var streamingMipmapsProperty = serializedTexture.FindProperty("m_StreamingMipmaps");
            var streamingMipmapsPriorityProperty = serializedTexture.FindProperty("m_StreamingMipmapsPriority");

            if (streamingMipmapsProperty != null)
                streamingMipmapsProperty.boolValue = true;
            if (streamingMipmapsPriorityProperty != null)
                streamingMipmapsPriorityProperty.intValue = 0;

            serializedTexture.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 圧縮後にMipStreamingフラグだけを再設定する（Reinitializeなし）
        /// </summary>
        private static void SetMipStreamingFlag(Texture2D texture)
        {
            if (texture == null) return;

            using var serializedTexture = new UnityEditor.SerializedObject(texture);
            var streamingMipmapsProperty = serializedTexture.FindProperty("m_StreamingMipmaps");
            if (streamingMipmapsProperty != null)
                streamingMipmapsProperty.boolValue = true;
            serializedTexture.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material[] GetOrCloneMaterials(Renderer renderer, Dictionary<Renderer, Material[]> cache)
        {
            if (renderer == null)
            {
                return Array.Empty<Material>();
            }

            if (cache.TryGetValue(renderer, out var cached) && cached != null)
            {
                return cached;
            }

            var materials = renderer.sharedMaterials;
            var cloned = materials != null ? materials.ToArray() : Array.Empty<Material>();
            cache[renderer] = cloned;
            return cloned;
        }

        private static void ApplyPostAdjustments(TextureColorAdjustmentComponent component, Texture2D texture)
        {
            const float epsilon = 0.001f;
            bool hasAdjustments = Mathf.Abs(component.hueShift) > epsilon
                || Mathf.Abs(component.saturation - 1f) > epsilon
                || Mathf.Abs(component.brightness) > epsilon
                || Mathf.Abs(component.gamma - 1f) > epsilon
                || Mathf.Abs(component.midtoneShift) > epsilon;

            if (!hasAdjustments)
                return;

            Color[] pixels = TextureUtils.GetPixelsSafe(texture);
            if (pixels == null)
                return;

            var adjusted = ColorSpaceConverter.ApplyHSBGToArray(
                pixels, component.hueShift, component.saturation, 1f, component.gamma);

            // Apply brightness offset (additive)
            if (Mathf.Abs(component.brightness) > epsilon)
            {
                for (int i = 0; i < adjusted.Length; i++)
                {
                    adjusted[i].r = Mathf.Clamp01(adjusted[i].r + component.brightness);
                    adjusted[i].g = Mathf.Clamp01(adjusted[i].g + component.brightness);
                    adjusted[i].b = Mathf.Clamp01(adjusted[i].b + component.brightness);
                }
            }

            // Apply midtone shift
            if (Mathf.Abs(component.midtoneShift) > epsilon)
            {
                for (int i = 0; i < adjusted.Length; i++)
                {
                    float shift = component.midtoneShift;
                    float wr = 4f * adjusted[i].r * (1f - adjusted[i].r);
                    float wg = 4f * adjusted[i].g * (1f - adjusted[i].g);
                    float wb = 4f * adjusted[i].b * (1f - adjusted[i].b);
                    adjusted[i].r = Mathf.Clamp01(adjusted[i].r + shift * wr);
                    adjusted[i].g = Mathf.Clamp01(adjusted[i].g + shift * wg);
                    adjusted[i].b = Mathf.Clamp01(adjusted[i].b + shift * wb);
                }
            }

            TextureUtils.SetPixelsSafe(texture, adjusted);
        }

        private static void ReplaceTexturesInMaterial(Material material, Dictionary<Texture2D, Texture2D> replacementMap)
        {
            if (material == null || replacementMap == null || replacementMap.Count == 0)
            {
                return;
            }

            var shader = material.shader;
            if (shader == null)
            {
                return;
            }

            int propertyCount = shader.GetPropertyCount();
            for (int i = 0; i < propertyCount; i++)
            {
                if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture)
                {
                    continue;
                }

                string propertyName = shader.GetPropertyName(i);
                if (material.GetTexture(propertyName) is Texture2D existingTexture &&
                    replacementMap.TryGetValue(existingTexture, out var replacement))
                {
                    material.SetTexture(propertyName, replacement);
                }
            }
        }

        private static void RegisterBakedAsset(BuildContext context, UnityEngine.Object asset, string baseName)
        {
            if (context?.AssetContainer == null || asset == null)
            {
                return;
            }

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            asset.name = $"{baseName}_{suffix}";

            try
            {
                AssetDatabase.AddObjectToAsset(asset, context.AssetContainer);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TexColAdjuster] Failed to register baked asset '{asset.name}': {ex.Message}");
            }
        }
    }
}
