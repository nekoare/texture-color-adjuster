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

            try
            {
                // Create readable copy without modifying original texture import settings
                readableTexture = TextureProcessor.MakeReadableCopy(originalTexture);
                if (readableTexture == null)
                {
                    Debug.LogError($"[TexColorAdjuster] Failed to create readable copy of texture: {originalTexture?.name ?? "null"}");
                    return null;
                }

                // Use non-destructive copy for reference texture to avoid modifying import settings
                readableReference = TextureProcessor.MakeReadableCopy(component.referenceTexture);
                if (readableReference == null)
                {
                    Debug.LogError($"[TexColorAdjuster] Failed to create readable copy of reference texture: {component.referenceTexture?.name ?? "null"}");
                    return null;
                }

                if (component.useHighPrecisionMode)
                {
                    // High precision mode processing
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
                        // Apply post-adjustment parameters (hue, saturation, brightness, gamma)
                        if (result != null)
                        {
                            try
                            {
                                Color[] pixels = TextureUtils.GetPixelsSafe(result);
                                if (pixels != null)
                                {
                                    var adjusted = TexColAdjuster.Editor.ColorSpaceConverter.ApplyHSBGToArray(
                                        pixels,
                                        component.hueShift,
                                        component.saturation,
                                        component.brightness,
                                        component.gamma
                                    );
                                    TextureUtils.SetPixelsSafe(result, adjusted);
                                }
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"[TexColorAdjuster] Failed to apply post-adjustments: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        result = HighPrecisionProcessor.ProcessWithHighPrecision(
                            readableTexture,
                            readableReference,
                            highPrecisionConfig,
                            component.intensity * 100f, // Convert back to 0-100 range for processor
                            component.preserveLuminance,
                            component.adjustmentMode
                        );
                    }
                }
                else if (component.useDualColorSelection)
                {
                    result = ColorAdjuster.AdjustColorsWithDualSelection(
                        readableTexture,
                        readableReference,
                        component.targetColor,
                        component.referenceColor,
                        component.intensity,
                        component.preserveLuminance,
                        component.adjustmentMode,
                        component.selectionRange
                    );
                }
                else
                {
                    result = ColorAdjuster.AdjustColors(
                        readableTexture,
                        readableReference,
                        component.intensity,
                        component.preserveLuminance,
                        component.adjustmentMode
                    );
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
            }

            if (result != null)
            {
                var textureAssetName = $"{originalTexture.name}_TexColAdjusted_{component.gameObject.name}";
                RegisterBakedAsset(context, result, textureAssetName);
            }

            return result;
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
