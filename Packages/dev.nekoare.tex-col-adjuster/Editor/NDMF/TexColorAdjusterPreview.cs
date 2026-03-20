using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using TexColAdjuster.Runtime;
using TexColAdjuster.Editor.Models;
using UnityEngine;
using UnityEngine.Rendering;
using TexColAdjuster;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TexColAdjuster.Editor.NDMF
{
    internal class TexColorAdjusterPreview : IRenderFilter
    {
        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            var renderGroups = new List<RenderGroup>();

            // Find all avatar roots in the scene
            var avatars = context.GetAvatarRoots();

            foreach (var avatar in avatars)
            {
                try
                {
                    // Get all TextureColorAdjustmentComponents in the avatar
                    var components = context.GetComponentsInChildren<TextureColorAdjustmentComponent>(avatar, true);

                    if (components.Length == 0)
                        continue;

                    // Observe specific properties to minimize unnecessary recalculations
                    foreach (var component in components)
                    {
                        // Observe only the properties that affect the preview
                        context.Observe(component, c => c.BindingVersion);
                        context.Observe(component, c => c.referenceTexture);
                        context.Observe(component, c => c.adjustmentMode);
                        context.Observe(component, c => c.intensity);
                        context.Observe(component, c => c.preserveLuminance);
                        context.Observe(component, c => c.useDualColorSelection);
                        context.Observe(component, c => c.targetColor);
                        context.Observe(component, c => c.referenceColor);
                        context.Observe(component, c => c.selectionRange);
                        // Observe post-adjustment parameters so preview updates when changed
                        context.Observe(component, c => c.hueShift);
                        context.Observe(component, c => c.saturation);
                        context.Observe(component, c => c.brightness);
                        context.Observe(component, c => c.gamma);
                        context.Observe(component, c => c.midtoneShift);
                        context.Observe(component, c => c.applyDuringBuild);
                        context.Observe(component, c => c.PreviewEnabled);
                        context.Observe(component, c => c.PreviewOnCPU);
                        context.Observe(component, c => c.useHighPrecisionMode);
                        context.Observe(component, c => c.highPrecisionReferenceObject);
                        context.Observe(component, c => c.highPrecisionMaterialIndex);
                        context.Observe(component, c => c.highPrecisionUVChannel);
                        context.Observe(component, c => c.highPrecisionDominantColorCount);
                        context.Observe(component, c => c.highPrecisionUseWeightedSampling);
                        context.Observe(component, c => c.highPrecisionMaskTexture);
                        context.Observe(component, c => c.highPrecisionMaskThreshold);
                        context.ActiveInHierarchy(component.gameObject);
                    }

                    // Get all target textures from enabled components
                    var targetTextures = components
                        .Where(c => c.PreviewEnabled && c.applyDuringBuild)
                        .SelectMany(EnumerateTargetTextures)
                        .Where(t => t != null)
                        .Distinct()
                        .ToArray();

                    if (targetTextures.Length == 0)
                        continue;

                    // Find all renderers in the avatar
                    var avatarRenderers = context.GetComponentsInChildren<Renderer>(avatar, true);

                    // Find renderers that use any of the target textures
                    var matchingRenderers = new List<Renderer>();

                    foreach (var renderer in avatarRenderers)
                    {
                        if (!context.ActiveInHierarchy(renderer.gameObject))
                            continue;

                        var materials = renderer.sharedMaterials;
                        bool hasTargetTexture = false;

                        foreach (var mat in materials)
                        {
                            if (mat == null)
                                continue;

                            if (MaterialUsesAnyTexture(mat, targetTextures))
                            {
                                hasTargetTexture = true;
                                break;
                            }
                        }

                        if (hasTargetTexture)
                        {
                            matchingRenderers.Add(renderer);
                        }
                    }

                    if (matchingRenderers.Count > 0)
                    {
                        renderGroups.Add(RenderGroup.For(matchingRenderers).WithData((avatar, components)));
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TexColorAdjuster Preview] Failed to process avatar '{avatar.name}': {ex.Message}");
                }
            }

            return renderGroups.ToImmutableList();
        }

        private class PreviewCacheEntry
        {
            public Material Material;
            public Texture Texture;
            public int Hash;          // Full hash (stage1 + stage2)
            public int Stage1Hash;    // Color conversion params only
            public Texture2D Stage1Result; // Cached color conversion result (before HSBG)
            public bool OwnsStage1;
            public int RefCount;
            public bool OwnsTexture;
            public int CacheKey;
            public bool IsStale;
        }

        private static readonly Dictionary<int, PreviewCacheEntry> s_PreviewCache = new Dictionary<int, PreviewCacheEntry>();

        // Cache for readable copies of reference textures (keyed by instance ID)
        private static readonly Dictionary<int, (Texture2D copy, int refCount)> s_ReferenceTextureCache
            = new Dictionary<int, (Texture2D, int)>();



        private static Texture2D AcquireReadableReference(Texture2D referenceTexture)
        {
            if (referenceTexture == null)
                return null;

            int key = referenceTexture.GetInstanceID();

            if (s_ReferenceTextureCache.TryGetValue(key, out var entry))
            {
                // Check if cached copy is still valid (Unity object not destroyed)
                if (entry.copy != null)
                {
                    s_ReferenceTextureCache[key] = (entry.copy, entry.refCount + 1);
                    return entry.copy;
                }
                // Cached copy was destroyed externally, remove stale entry
                s_ReferenceTextureCache.Remove(key);
            }

            var copy = TextureProcessor.MakeReadableCopy(referenceTexture);
            if (copy != null)
            {
                s_ReferenceTextureCache[key] = (copy, 1);
            }
            return copy;
        }

        private static void ReleaseReadableReference(Texture2D referenceTexture)
        {
            if (referenceTexture == null)
                return;

            int key = referenceTexture.GetInstanceID();

            if (!s_ReferenceTextureCache.TryGetValue(key, out var entry))
                return;

            int newCount = entry.refCount - 1;
            if (newCount <= 0)
            {
                if (entry.copy != null)
                {
                    UnityEngine.Object.DestroyImmediate(entry.copy);
                }
                s_ReferenceTextureCache.Remove(key);
            }
            else
            {
                s_ReferenceTextureCache[key] = (entry.copy, newCount);
            }
        }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            try
            {
                var (avatar, components) = group.GetData<(GameObject, TextureColorAdjustmentComponent[])>();

                // Filter to enabled components only
                var enabledComponents = components
                    .Where(c => c.applyDuringBuild && c.PreviewEnabled && context.ActiveInHierarchy(c.gameObject))
                    .ToList();

                if (!enabledComponents.Any())
                {
                    return Task.FromResult<IRenderFilterNode>(new EmptyNode());
                }

                var rendererOverrides = new Dictionary<Renderer, Material[]>();
                var usedCacheEntries = new HashSet<PreviewCacheEntry>();
                bool gpuAvailable = GPUColorAdjuster.IsGPUProcessingAvailable();

                foreach (var component in enabledComponents)
                {
                    foreach (var binding in component.EnumerateValidBindings())
                    {
                        var renderer = binding.renderer;
                        if (renderer == null)
                            continue;

                        if (!context.ActiveInHierarchy(renderer.gameObject))
                            continue;

                        var materials = renderer.sharedMaterials;
                        if (materials == null)
                            continue;

                        int slot = binding.materialSlot;
                        if (slot < 0 || slot >= materials.Length)
                            continue;

                        var originalMaterial = materials[slot];
                        if (originalMaterial == null)
                            continue;

                        var originalTexture = GetTargetTexture(binding);
                        if (originalTexture == null)
                            continue;

                        int cacheKey = BuildCacheKey(component, binding);
                        int stage1Hash = ComputeStage1Hash(component, binding, originalTexture);
                        int stateHash = ComputeComponentStateHash(component, binding, originalTexture);

                        var cacheEntry = AcquireCacheEntry(component, originalMaterial, originalTexture, gpuAvailable, cacheKey, stateHash, stage1Hash);
                        if (cacheEntry == null)
                            continue;

                        usedCacheEntries.Add(cacheEntry);

                        if (!rendererOverrides.TryGetValue(renderer, out var overrideMaterials))
                        {
                            overrideMaterials = (Material[])materials.Clone();
                            rendererOverrides.Add(renderer, overrideMaterials);
                        }

                        overrideMaterials[slot] = cacheEntry.Material;
                    }
                }

                if (rendererOverrides.Count == 0)
                {
                    foreach (var entry in usedCacheEntries)
                    {
                        ReleaseCacheEntry(entry);
                    }

                    return Task.FromResult<IRenderFilterNode>(new EmptyNode());
                }

                return Task.FromResult<IRenderFilterNode>(
                    new RendererScopedMaterialOverrideNode(rendererOverrides, usedCacheEntries));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TexColorAdjuster Preview] Failed to instantiate preview: {ex.Message}\n{ex.StackTrace}");
                return Task.FromResult<IRenderFilterNode>(new EmptyNode());
            }
        }

        // Stage1 hash: color conversion parameters (expensive to recompute)
        private static int ComputeStage1Hash(TextureColorAdjustmentComponent component, TextureColorAdjustmentComponent.TargetBinding binding, Texture originalTexture)
        {
            if (component == null)
                return 0;

            int hash = 17;

            hash = CombineHash(hash, component.adjustmentMode.GetHashCode());
            hash = CombineHash(hash, FloatToHash(component.intensity));
            hash = CombineHash(hash, component.preserveLuminance ? 1 : 0);
            hash = CombineHash(hash, component.useDualColorSelection ? 1 : 0);
            hash = CombineHash(hash, ColorToHash(component.targetColor));
            hash = CombineHash(hash, ColorToHash(component.referenceColor));
            hash = CombineHash(hash, FloatToHash(component.selectionRange));
            hash = CombineHash(hash, component.useHighPrecisionMode ? 1 : 0);
            hash = CombineHash(hash, component.highPrecisionReferenceObject ? component.highPrecisionReferenceObject.GetInstanceID() : 0);
            hash = CombineHash(hash, component.highPrecisionMaterialIndex);
            hash = CombineHash(hash, component.highPrecisionUVChannel);
            hash = CombineHash(hash, component.highPrecisionDominantColorCount);
            hash = CombineHash(hash, component.highPrecisionUseWeightedSampling ? 1 : 0);
            hash = CombineHash(hash, component.highPrecisionMaskTexture ? component.highPrecisionMaskTexture.GetInstanceID() : 0);
            hash = CombineHash(hash, FloatToHash(component.highPrecisionMaskThreshold));
#if UNITY_EDITOR
            hash = CombineHash(hash, component.highPrecisionMaskTexture ? component.highPrecisionMaskTexture.imageContentsHash.GetHashCode() : 0);
#endif
            hash = CombineHash(hash, component.PreviewOnCPU ? 1 : 0);
            hash = CombineHash(hash, binding.renderer ? binding.renderer.GetInstanceID() : 0);
            hash = CombineHash(hash, binding.materialSlot);
            hash = CombineHash(hash, component.referenceTexture ? component.referenceTexture.GetInstanceID() : 0);
            hash = CombineHash(hash, originalTexture ? originalTexture.GetInstanceID() : 0);

            return hash;
        }

        // Full hash including Stage2 (HSBG post-adjustment parameters)
        private static int ComputeComponentStateHash(TextureColorAdjustmentComponent component, TextureColorAdjustmentComponent.TargetBinding binding, Texture originalTexture)
        {
            int hash = ComputeStage1Hash(component, binding, originalTexture);

            hash = CombineHash(hash, FloatToHash(component.hueShift));
            hash = CombineHash(hash, FloatToHash(component.saturation));
            hash = CombineHash(hash, FloatToHash(component.brightness));
            hash = CombineHash(hash, FloatToHash(component.gamma));
            hash = CombineHash(hash, FloatToHash(component.midtoneShift));

            return hash;
        }

        private PreviewCacheEntry AcquireCacheEntry(
            TextureColorAdjustmentComponent component,
            Material originalMaterial,
            Texture2D originalTexture,
            bool gpuAvailable,
            int cacheKey,
            int stateHash,
            int stage1Hash)
        {
            if (component == null || originalMaterial == null || originalTexture == null)
                return null;

            if (s_PreviewCache.TryGetValue(cacheKey, out var entry))
            {
                // Full cache hit — nothing changed
                if (entry != null && !entry.IsStale && entry.Hash == stateHash && entry.Material != null && entry.Texture != null)
                {
                    entry.CacheKey = cacheKey;
                    entry.IsStale = false;
                    entry.RefCount++;
                    return entry;
                }

                // Stage1 hit — only HSBG changed, reuse Stage1 result
                if (entry != null && !entry.IsStale && entry.Stage1Hash == stage1Hash
                    && entry.Stage1Result != null && entry.Material != null)
                {
                    // Duplicate Stage1 result and apply new HSBG
                    var newResult = TextureProcessor.MakeReadableCopy(entry.Stage1Result);
                    if (newResult != null)
                    {
                        try
                        {
                            ApplyPostAdjustments(component, newResult);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[TexColorAdjuster Preview] Failed to apply HSBG on stage1 cache: {ex.Message}");
                        }

                        newResult.hideFlags = HideFlags.HideAndDontSave;

                        // Destroy old final texture
                        if (entry.OwnsTexture && entry.Texture != null)
                        {
                            DestroyTexture(entry.Texture);
                        }

                        // Update material to use new texture
                        var shader = entry.Material.shader;
                        if (shader != null)
                        {
                            int propertyCount = UnityEditor.ShaderUtil.GetPropertyCount(shader);
                            for (int i = 0; i < propertyCount; i++)
                            {
                                if (UnityEditor.ShaderUtil.GetPropertyType(shader, i) != UnityEditor.ShaderUtil.ShaderPropertyType.TexEnv)
                                    continue;
                                string propName = UnityEditor.ShaderUtil.GetPropertyName(shader, i);
                                if (entry.Material.GetTexture(propName) == entry.Texture)
                                {
                                    entry.Material.SetTexture(propName, newResult);
                                }
                            }
                        }

                        entry.Texture = newResult;
                        entry.OwnsTexture = true;
                        entry.Hash = stateHash;
                        entry.RefCount++;
                        return entry;
                    }
                }

                if (entry != null)
                {
                    entry.IsStale = true;
                }

                s_PreviewCache.Remove(cacheKey);
            }

            var processedTexture = ProcessTextureForComponent(component, originalTexture, gpuAvailable);
            if (processedTexture == null)
                return null;

            bool ownsTexture = !ReferenceEquals(processedTexture, originalTexture);
            if (ownsTexture)
            {
                processedTexture.hideFlags = HideFlags.HideAndDontSave;
            }

            // Save Stage1 result (before HSBG) for future partial cache hits
            Texture2D stage1Copy = null;
            bool ownsStage1 = false;
            if (HasPostAdjustments(component) && processedTexture is Texture2D processedTex2D)
            {
                stage1Copy = TextureProcessor.MakeReadableCopy(processedTex2D);
                if (stage1Copy != null)
                {
                    stage1Copy.hideFlags = HideFlags.HideAndDontSave;
                    ownsStage1 = true;
                }
            }

            var previewMaterial = CreatePreviewMaterial(originalMaterial, originalTexture, processedTexture);
            if (previewMaterial == null)
            {
                if (ownsTexture)
                {
                    DestroyTexture(processedTexture);
                }
                if (ownsStage1 && stage1Copy != null)
                {
                    UnityEngine.Object.DestroyImmediate(stage1Copy);
                }
                return null;
            }

            previewMaterial.hideFlags = HideFlags.HideAndDontSave;

            entry = new PreviewCacheEntry
            {
                Material = previewMaterial,
                Texture = processedTexture,
                Hash = stateHash,
                Stage1Hash = stage1Hash,
                Stage1Result = stage1Copy,
                OwnsStage1 = ownsStage1,
                RefCount = 1,
                OwnsTexture = ownsTexture,
                CacheKey = cacheKey,
                IsStale = false
            };

            s_PreviewCache[cacheKey] = entry;
            return entry;
        }

        private IEnumerable<Texture2D> EnumerateTargetTextures(TextureColorAdjustmentComponent component)
        {
            if (component == null)
                yield break;

            foreach (var binding in component.EnumerateValidBindings())
            {
                var texture = GetTargetTexture(binding);
                if (texture != null)
                    yield return texture;
            }
        }

        private Texture2D GetTargetTexture(TextureColorAdjustmentComponent.TargetBinding binding)
        {
            if (!binding.IsValid)
                return null;

            var renderer = binding.renderer;
            if (renderer == null)
                return null;

            var materials = renderer.sharedMaterials;
            if (materials == null)
                return null;

            int slot = binding.materialSlot;
            if (slot < 0 || slot >= materials.Length)
                return null;

            var material = materials[slot];
            if (material == null)
                return null;

            return material.GetTexture("_MainTex") as Texture2D;
        }

        private int BuildCacheKey(TextureColorAdjustmentComponent component, TextureColorAdjustmentComponent.TargetBinding binding)
        {
            unchecked
            {
                int hash = component != null ? component.GetInstanceID() : 0;
                hash = (hash * 397) ^ (binding.renderer ? binding.renderer.GetInstanceID() : 0);
                hash = (hash * 397) ^ binding.materialSlot;
                return hash;
            }
        }

        private bool MaterialUsesAnyTexture(Material material, Texture2D[] textures)
        {
            if (material == null || textures == null || textures.Length == 0)
                return false;

            var shader = material.shader;
            if (shader == null)
                return false;

#if UNITY_EDITOR
            int propertyCount = ShaderUtil.GetPropertyCount(shader);

            for (int i = 0; i < propertyCount; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                    continue;

                string propName = ShaderUtil.GetPropertyName(shader, i);
#else
            int propertyCount = shader.GetPropertyCount();

            for (int i = 0; i < propertyCount; i++)
            {
                if (shader.GetPropertyType(i) != ShaderPropertyType.Texture)
                    continue;

                string propName = shader.GetPropertyName(i);
#endif
                var tex = material.GetTexture(propName);

                if (textures.Contains(tex))
                    return true;
            }

            return false;
        }

        private Texture ProcessTextureCPU(TextureColorAdjustmentComponent component, Texture2D originalTexture)
        {
            Texture2D readableTexture = null;
            Texture2D readableReference = null;
            bool referenceFromCache = false;
            try
            {
                readableTexture = TextureProcessor.MakeReadableCopy(originalTexture);
                if (readableTexture == null)
                {
                    Debug.LogError($"[TexColorAdjuster Preview] Failed to create readable copy of texture: {originalTexture?.name ?? "null"}");
                    return null;
                }

                // Use cached readable copy for reference texture (avoids repeated GPU Blit + ReadPixels)
                readableReference = AcquireReadableReference(component.referenceTexture);
                referenceFromCache = readableReference != null;
                if (readableReference == null)
                {
                    Debug.LogError($"[TexColorAdjuster Preview] Failed to create readable copy of reference texture: {component.referenceTexture?.name ?? "null"}");
                    return null;
                }

                Texture2D result;
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
                        Debug.LogWarning($"[TexColorAdjuster Preview] High precision mode is enabled but configuration is invalid. Falling back to standard processing.");
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
                            component.intensity * 100f, // Convert back to 0-100 range for processor
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
                            UnityEngine.Object.DestroyImmediate(result);
                            result = refined;
                        }
                    }
                }

                // Apply post-adjustment parameters (skip if all defaults)
                if (result != null)
                {
                    try
                    {
                        ApplyPostAdjustments(component, result);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[TexColorAdjuster Preview] Failed to apply post-adjustments (CPU): {ex.Message}");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TexColorAdjuster Preview] Failed to process texture (CPU): {ex.Message}");
                return null;
            }
            finally
            {
                // Clean up temporary textures
                if (readableTexture != null && readableTexture != originalTexture)
                {
                    UnityEngine.Object.DestroyImmediate(readableTexture);
                }
                // Reference texture: release cache ref instead of destroying
                if (referenceFromCache)
                {
                    ReleaseReadableReference(component.referenceTexture);
                }
                else if (readableReference != null && readableReference != component.referenceTexture)
                {
                    UnityEngine.Object.DestroyImmediate(readableReference);
                }
            }
        }

        private Texture ProcessTextureGPU(TextureColorAdjustmentComponent component, Texture2D originalTexture)
        {
            try
            {
                // GPU path does not handle high precision mode
                if (component.useHighPrecisionMode)
                {
                    return ProcessTextureCPU(component, originalTexture);
                }

                // Check if GPU processing is available
                if (!GPUColorAdjuster.IsGPUProcessingAvailable())
                {
                    return ProcessTextureCPU(component, originalTexture);
                }

                // Attempt GPU processing
                Texture result = GPUColorAdjuster.AdjustColorsGPU(
                    originalTexture,
                    component.referenceTexture,
                    component.intensity,
                    component.preserveLuminance,
                    component.adjustmentMode
                );

                // Fallback to CPU if GPU processing failed
                if (result == null)
                {
                    return ProcessTextureCPU(component, originalTexture);
                }

                // Apply HSBG on GPU if needed
                Texture postResult = result;
                if (HasPostAdjustments(component) && result is RenderTexture gpuRT && GPUColorAdjuster.IsHSBGGPUAvailable())
                {
                    try
                    {
                        var hsbgResult = GPUColorAdjuster.ApplyHSBGOnGPU(
                            gpuRT,
                            component.hueShift,
                            component.saturation,
                            1f,
                            component.gamma,
                            component.brightness,
                            1f,
                            component.midtoneShift
                        );

                        if (hsbgResult != null)
                        {
                            try { if (result is IDisposable d) d.Dispose(); } catch { }
                            postResult = hsbgResult;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[TexColorAdjuster Preview] GPU HSBG failed: {ex.Message}");
                    }
                }

                // No CPU work needed — return GPU result directly
                if (!component.useDualColorSelection && !HasMidtoneShift(component))
                {
                    return postResult;
                }

                // CPU readback for DualSelection / midtone shift
                try
                {
                    Texture2D readable = null;

                    if (postResult is RenderTexture rt)
                    {
                        var prev = RenderTexture.active;
                        RenderTexture.active = rt;
                        readable = TextureColorSpaceUtility.CreateRuntimeTextureLike(rt);
                        readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                        readable.Apply();
                        RenderTexture.active = prev;
                    }
                    else if (postResult is Texture2D tex2d)
                    {
                        readable = TextureProcessor.MakeReadableCopy(tex2d);
                    }

                    try { if (postResult is IDisposable d2) d2.Dispose(); } catch { }

                    if (readable != null)
                    {
                        // DualSelection refinement (CPU)
                        if (component.useDualColorSelection)
                        {
                            var refined = ColorAdjuster.ApplyDualSelectionRefinement(
                                readable, component.targetColor, component.referenceColor, component.selectionRange);
                            if (refined != null)
                            {
                                TextureColorSpaceUtility.UnregisterRuntimeTexture(readable);
                                UnityEngine.Object.DestroyImmediate(readable);
                                readable = refined;
                            }
                        }

                        // Midtone shift (CPU, not handled by GPU HSBG)
                        if (HasMidtoneShift(component))
                        {
                            ApplyMidtoneShiftToTexture(component, readable);
                        }

                        return readable;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[TexColorAdjuster Preview] Error in CPU post-processing: {e.Message}");
                }

                // If conversion failed, fallback to CPU processing result
                return ProcessTextureCPU(component, originalTexture);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TexColorAdjuster Preview] GPU processing failed: {ex.Message}. Falling back to CPU.");
                return ProcessTextureCPU(component, originalTexture);
            }
        }

        private static bool HasPostAdjustments(TextureColorAdjustmentComponent component)
        {
            const float epsilon = 0.001f;
            return Mathf.Abs(component.hueShift) > epsilon
                || Mathf.Abs(component.saturation - 1f) > epsilon
                || Mathf.Abs(component.brightness) > epsilon
                || Mathf.Abs(component.gamma - 1f) > epsilon
                || Mathf.Abs(component.midtoneShift) > epsilon;
        }

        private static void ApplyPostAdjustments(TextureColorAdjustmentComponent component, Texture2D texture)
        {
            if (!HasPostAdjustments(component))
                return;

            Color[] pixels = TextureUtils.GetPixelsSafe(texture);
            if (pixels == null)
                return;

            var adjusted = TexColAdjuster.Editor.ColorSpaceConverter.ApplyHSBGToArray(
                pixels,
                component.hueShift,
                component.saturation,
                1f,
                component.gamma
            );

            // Apply brightness offset (additive)
            const float epsilon = 0.001f;
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
                float shift = component.midtoneShift;
                for (int i = 0; i < adjusted.Length; i++)
                {
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

        private static bool HasMidtoneShift(TextureColorAdjustmentComponent component)
        {
            return Mathf.Abs(component.midtoneShift) > 0.001f;
        }

        private static void ApplyMidtoneShiftToTexture(TextureColorAdjustmentComponent component, Texture2D texture)
        {
            Color[] pixels = TextureUtils.GetPixelsSafe(texture);
            if (pixels == null) return;

            float shift = component.midtoneShift;
            for (int i = 0; i < pixels.Length; i++)
            {
                float wr = 4f * pixels[i].r * (1f - pixels[i].r);
                float wg = 4f * pixels[i].g * (1f - pixels[i].g);
                float wb = 4f * pixels[i].b * (1f - pixels[i].b);
                pixels[i].r = Mathf.Clamp01(pixels[i].r + shift * wr);
                pixels[i].g = Mathf.Clamp01(pixels[i].g + shift * wg);
                pixels[i].b = Mathf.Clamp01(pixels[i].b + shift * wb);
            }
            TextureUtils.SetPixelsSafe(texture, pixels);
        }

        private Texture ProcessTextureForComponent(TextureColorAdjustmentComponent component, Texture2D originalTexture, bool gpuAvailable)
        {
            bool requireCpu = component.PreviewOnCPU || component.useHighPrecisionMode || !gpuAvailable;
            if (requireCpu)
            {
                return ProcessTextureCPU(component, originalTexture);
            }

            return ProcessTextureGPU(component, originalTexture);
        }

        private Material CreatePreviewMaterial(Material originalMaterial, Texture2D originalTexture, Texture processedTexture)
        {
            if (originalMaterial == null || originalTexture == null || processedTexture == null)
                return null;

            var shader = originalMaterial.shader;
            if (shader == null)
                return null;

            var clonedMaterial = UnityEngine.Object.Instantiate(originalMaterial);
            clonedMaterial.name = originalMaterial.name + " (Preview)";

#if UNITY_EDITOR
            int propertyCount = ShaderUtil.GetPropertyCount(shader);

            for (int i = 0; i < propertyCount; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                    continue;

                string propName = ShaderUtil.GetPropertyName(shader, i);
#else
            int propertyCount = shader.GetPropertyCount();

            for (int i = 0; i < propertyCount; i++)
            {
                if (shader.GetPropertyType(i) != ShaderPropertyType.Texture)
                    continue;

                string propName = shader.GetPropertyName(i);
#endif
                var tex = originalMaterial.GetTexture(propName);

                if (tex == originalTexture)
                {
                    var scale = originalMaterial.GetTextureScale(propName);
                    var offset = originalMaterial.GetTextureOffset(propName);

                    clonedMaterial.SetTexture(propName, processedTexture);
                    clonedMaterial.SetTextureScale(propName, scale);
                    clonedMaterial.SetTextureOffset(propName, offset);
                }
            }

            return clonedMaterial;
        }

        private class RendererScopedMaterialOverrideNode : IRenderFilterNode, IDisposable
        {
            private readonly Dictionary<Renderer, Material[]> _rendererOverrides;
            private readonly HashSet<PreviewCacheEntry> _cacheEntries;

            public RenderAspects WhatChanged => RenderAspects.Material;

            public RendererScopedMaterialOverrideNode(Dictionary<Renderer, Material[]> rendererOverrides,
                HashSet<PreviewCacheEntry> cacheEntries)
            {
                _rendererOverrides = rendererOverrides;
                _cacheEntries = cacheEntries;
            }

            public void OnFrame(Renderer original, Renderer proxy)
            {
                if (original == null || proxy == null)
                    return;

                if (_rendererOverrides == null)
                    return;

                if (!_rendererOverrides.TryGetValue(original, out var materials) || materials == null)
                    return;

                proxy.sharedMaterials = materials;
            }

            public void Dispose()
            {
                if (_cacheEntries == null)
                    return;

                foreach (var entry in _cacheEntries)
                {
                    ReleaseCacheEntry(entry);
                }
            }
        }

        private static void ReleaseCacheEntry(PreviewCacheEntry entry)
        {
            if (entry == null)
                return;

            entry.RefCount--;

            if (entry.RefCount > 0)
                return;

            if (!entry.IsStale && s_PreviewCache.TryGetValue(entry.CacheKey, out var current) && ReferenceEquals(current, entry))
            {
                s_PreviewCache.Remove(entry.CacheKey);
            }

            DestroyCacheAssets(entry);
        }

        private static void DestroyCacheAssets(PreviewCacheEntry entry)
        {
            if (entry == null)
                return;

            if (entry.Material != null)
            {
                UnityEngine.Object.DestroyImmediate(entry.Material);
            }

            if (entry.OwnsTexture)
            {
                DestroyTexture(entry.Texture);
            }

            if (entry.OwnsStage1 && entry.Stage1Result != null)
            {
                UnityEngine.Object.DestroyImmediate(entry.Stage1Result);
            }

            entry.Material = null;
            entry.Texture = null;
            entry.Stage1Result = null;
            entry.OwnsTexture = false;
            entry.OwnsStage1 = false;
        }

        private static void DestroyTexture(Texture texture)
        {
            if (texture == null)
                return;

            if (texture is ExtendedRenderTexture ert)
            {
                ert.Dispose();
                return;
            }

            if (texture is RenderTexture rt)
            {
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                return;
            }

            if (texture is Texture2D tex2D)
            {
                TextureColorSpaceUtility.UnregisterRuntimeTexture(tex2D);
                UnityEngine.Object.DestroyImmediate(tex2D);
                return;
            }

            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static int CombineHash(int current, int value)
        {
            unchecked
            {
                return current * 31 + value;
            }
        }

        private static int FloatToHash(float value)
        {
            return Mathf.RoundToInt(value * 10000f);
        }

        private static int ColorToHash(Color color)
        {
            unchecked
            {
                int r = Mathf.RoundToInt(color.r * 1000f);
                int g = Mathf.RoundToInt(color.g * 1000f);
                int b = Mathf.RoundToInt(color.b * 1000f);
                int a = Mathf.RoundToInt(color.a * 1000f);

                int hash = 17;
                hash = hash * 31 + r;
                hash = hash * 31 + g;
                hash = hash * 31 + b;
                hash = hash * 31 + a;
                return hash;
            }
        }

        private class EmptyNode : IRenderFilterNode
        {
            public RenderAspects WhatChanged => 0;

            public void OnFrame(Renderer original, Renderer proxy)
            {
                // No-op
            }
        }
    }
}
