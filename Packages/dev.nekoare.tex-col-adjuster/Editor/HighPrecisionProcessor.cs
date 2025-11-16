using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using TexColAdjuster.Editor;
using TexColAdjuster.Runtime;

namespace TexColAdjuster
{
    // High-precision color processing that uses only mesh-used UV areas
    public static class HighPrecisionProcessor
    {
        // High-precision configuration
        [Serializable]
        public class HighPrecisionConfig
        {
            [Header("GameObject Settings")]
            public GameObject referenceGameObject;
            public int materialIndex = 0;
            [Range(0, 7)]
            public int uvChannel = 0;

            [Header("Masking Settings")]
            public bool showVisualMask = true;
            public Color maskColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            [Range(0f, 1f)]
            public float maskIntensity = 0.3f;

            [Header("Precomputed Mask")]
            public Texture2D maskTexture;
            [Range(0f, 1f)]
            public float maskThreshold = 0.5f;

            [Header("Color Extraction")]
            [Range(3, 10)]
            public int dominantColorCount = 5;
            public bool useWeightedSampling = true;
        }

        // Process color adjustment using high-precision mode
        public static Texture2D ProcessWithHighPrecision(Texture2D targetTexture, Texture2D referenceTexture,
            HighPrecisionConfig config, float intensity, bool preserveLuminance, ColorAdjustmentMode mode)
        {
            if (targetTexture == null || referenceTexture == null || config == null)
                return null;

            try
            {
                MeshUVAnalyzer.UVUsageData uvUsage = null;

                if (config.maskTexture != null)
                {
                    uvUsage = MeshUVAnalyzer.CreateUVUsageFromMask(config.maskTexture, config.maskThreshold);
                    if (uvUsage == null)
                    {
                        Debug.LogWarning("[High-precision] Failed to rebuild UV usage from precomputed mask. Falling back to mesh analysis.");
                    }
                }

                Texture2D analysisTexture = targetTexture;

                if (uvUsage == null)
                {
                    if (config.referenceGameObject == null)
                    {
                        Debug.LogError("High-precision mode requires either a reference GameObject or a precomputed mask texture.");
                        return null;
                    }

                    // Resolve the texture actually bound to the renderer so UV analysis targets the correct area
                    analysisTexture = ResolveAnalysisTexture(config, targetTexture);

                    // Analyze UV usage of the reference GameObject
                    uvUsage = MeshUVAnalyzer.AnalyzeGameObjectUVUsage(
                        config.referenceGameObject, analysisTexture, config.materialIndex, config.uvChannel);

                    if (uvUsage == null)
                    {
                        Debug.LogError("Failed to analyze UV usage for high-precision mode");
                        return null;
                    }
                }

                Debug.Log($"High-precision mode: Using {uvUsage.usagePercentage:F1}% of texture area ({uvUsage.usedUVs.Count} UV points)");

                // Extract colors only from used UV areas
                var dominantColors = MeshUVAnalyzer.ExtractDominantColorsFromUsedAreas(
                    referenceTexture, uvUsage, config.dominantColorCount);

                if (dominantColors.Count == 0)
                {
                    Debug.LogError("No dominant colors extracted from used UV areas");
                    return null;
                }

                // Create synthetic reference texture using only mesh-used colors
                var syntheticReference = CreateSyntheticReferenceFromUsedAreas(
                    referenceTexture, uvUsage, dominantColors, config);

                // Perform color adjustment using the synthetic reference
                var adjustedTexture = ProcessWithSyntheticReference(targetTexture, syntheticReference,
                    intensity, preserveLuminance, mode);

                if (adjustedTexture == null)
                    return null;

                // Apply UV mask so only the mesh's used area is modified
                return ApplyUVMask(adjustedTexture, targetTexture, uvUsage);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"High-precision processing failed: {e.Message}");
                return null;
            }
        }

        // Create a synthetic reference texture using only colors from mesh-used areas
        private static Color[] CreateSyntheticReferenceFromUsedAreas(Texture2D referenceTexture, 
            MeshUVAnalyzer.UVUsageData uvUsage, List<Color> dominantColors, HighPrecisionConfig config)
        {
            var readableTexture = TextureProcessor.MakeReadableCopy(referenceTexture);
            if (readableTexture == null) return null;

            try
            {
                Color[] originalPixels = TextureUtils.GetPixelsSafe(readableTexture);
                if (originalPixels == null) return null;

                Color[] syntheticPixels = new Color[originalPixels.Length];
                var random = new System.Random(42); // Fixed seed for consistency

                for (int i = 0; i < originalPixels.Length; i++)
                {
                    Color originalPixel = originalPixels[i];
                    
                    // If original pixel is transparent, preserve it completely
                    if (originalPixel.a < 0.01f)
                    {
                        syntheticPixels[i] = originalPixel;
                        continue;
                    }

                    if (i < uvUsage.usedPixels.Length && uvUsage.usedPixels[i])
                    {
                        // For used areas, use the original pixel color
                        syntheticPixels[i] = originalPixel;
                    }
                    else
                    {
                        // For unused areas, use dominant colors with weighted distribution
                        if (config.useWeightedSampling)
                        {
                            // Weight colors based on their importance
                            float totalWeight = 0f;
                            float[] weights = new float[dominantColors.Count];
                            
                            for (int j = 0; j < dominantColors.Count; j++)
                            {
                                // First color (most dominant) gets highest weight
                                weights[j] = 1f / (j + 1f);
                                totalWeight += weights[j];
                            }
                            
                            // Weighted random selection
                            float randomValue = (float)random.NextDouble() * totalWeight;
                            float currentWeight = 0f;
                            
                            for (int j = 0; j < dominantColors.Count; j++)
                            {
                                currentWeight += weights[j];
                                if (randomValue <= currentWeight)
                                {
                                    syntheticPixels[i] = dominantColors[j];
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // Simple random selection from dominant colors
                            int colorIndex = random.Next(dominantColors.Count);
                            syntheticPixels[i] = dominantColors[colorIndex];
                        }
                    }
                }
                return syntheticPixels;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(readableTexture);
            }
        }

        // Process color adjustment with synthetic reference
        private static Texture2D ProcessWithSyntheticReference(Texture2D targetTexture, Color[] syntheticReference,
            float intensity, bool preserveLuminance, ColorAdjustmentMode mode)
        {
            if (targetTexture == null || syntheticReference == null) return null;

            Color[] targetPixels = TextureUtils.GetPixelsSafe(targetTexture);
            if (targetPixels == null) return null;

            Color[] adjustedPixels = ColorAdjuster.AdjustColors(targetPixels, syntheticReference, 
                intensity / 100f, preserveLuminance, mode);

            if (adjustedPixels == null) return null;

            var resultTexture = TextureColorSpaceUtility.CreateRuntimeTextureLike(targetTexture);
            if (TextureUtils.SetPixelsSafe(resultTexture, adjustedPixels))
            {
                return resultTexture;
            }

            TextureColorSpaceUtility.UnregisterRuntimeTexture(resultTexture);
            UnityEngine.Object.DestroyImmediate(resultTexture);
            return null;
        }
        private static Texture2D ApplyUVMask(Texture2D adjustedTexture, Texture2D originalTexture, MeshUVAnalyzer.UVUsageData uvUsage)
        {
            if (adjustedTexture == null || originalTexture == null || uvUsage == null || uvUsage.usedPixels == null)
                return adjustedTexture;

            var adjustedPixels = TextureUtils.GetPixelsSafe(adjustedTexture);
            var originalPixels = TextureUtils.GetPixelsSafe(originalTexture);

            if (adjustedPixels == null || originalPixels == null)
                return adjustedTexture;

            if (uvUsage.usedPixels.Length != adjustedPixels.Length)
                return adjustedTexture;

            var blendedPixels = new Color[adjustedPixels.Length];
            for (int i = 0; i < blendedPixels.Length; i++)
            {
                blendedPixels[i] = uvUsage.usedPixels[i] ? adjustedPixels[i] : originalPixels[i];
            }

            TextureUtils.SetPixelsSafe(adjustedTexture, blendedPixels);
            return adjustedTexture;
        }

        // Create preview texture with visual mask overlay
        public static Texture2D CreateHighPrecisionPreview(Texture2D referenceTexture,
            HighPrecisionConfig config, bool showMask = true)
        {
            if (referenceTexture == null || config == null)
                return null;

            try
            {
                MeshUVAnalyzer.UVUsageData uvUsage = null;
                Texture2D previewBaseTexture = referenceTexture;

                if (config.maskTexture != null)
                {
                    uvUsage = MeshUVAnalyzer.CreateUVUsageFromMask(config.maskTexture, config.maskThreshold);
                }

                if (uvUsage == null)
                {
                    if (config.referenceGameObject == null)
                        return null;

                    var analysisTexture = ResolveAnalysisTexture(config, referenceTexture);
                    uvUsage = MeshUVAnalyzer.AnalyzeGameObjectUVUsage(
                        config.referenceGameObject, analysisTexture, config.materialIndex, config.uvChannel);

                    if (uvUsage == null)
                        return null;

                    previewBaseTexture = analysisTexture ?? referenceTexture;
                }

                if (showMask && config.showVisualMask && previewBaseTexture != null)
                {
                    return MeshUVAnalyzer.CreateMaskedTexture(previewBaseTexture, uvUsage,
                        config.maskColor, config.maskIntensity);
                }

                return previewBaseTexture;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to create high-precision preview: {e.Message}");
                return null;
            }
        }

        // Get UV usage statistics for display
        public static string GetUVUsageStatistics(Texture2D referenceTexture, HighPrecisionConfig config)
        {
            if (referenceTexture == null || config == null)
                return "No data available";

            try
            {
                MeshUVAnalyzer.UVUsageData uvUsage = null;

                if (config.maskTexture != null)
                {
                    uvUsage = MeshUVAnalyzer.CreateUVUsageFromMask(config.maskTexture, config.maskThreshold);
                }

                if (uvUsage == null)
                {
                    if (config.referenceGameObject == null)
                        return "No data available";

                    var analysisTexture = ResolveAnalysisTexture(config, referenceTexture);
                    uvUsage = MeshUVAnalyzer.AnalyzeGameObjectUVUsage(
                        config.referenceGameObject, analysisTexture, config.materialIndex, config.uvChannel);
                }

                if (uvUsage == null)
                    return "Failed to analyze UV usage";

                var bounds = uvUsage.uvBounds;
                return $"UV Usage: {uvUsage.usagePercentage:F1}%\n" +
                       $"Used Triangles: {uvUsage.usedUVs.Count / 3}\n" +
                       $"UV Bounds: ({bounds.min.x:F2}, {bounds.min.y:F2}) - ({bounds.max.x:F2}, {bounds.max.y:F2})\n" +
                       $"Coverage Area: {bounds.size.x:F2} × {bounds.size.y:F2}";
            }
            catch (System.Exception e)
            {
                return $"Error: {e.Message}";
            }
        }

        // Extract target color from high-precision reference
        public static Color ExtractHighPrecisionTargetColor(Texture2D referenceTexture, 
            HighPrecisionConfig config, Vector2 normalizedPosition)
        {
            if (referenceTexture == null || config == null)
                return Color.white;

            try
            {
                MeshUVAnalyzer.UVUsageData uvUsage = null;

                if (config.maskTexture != null)
                {
                    uvUsage = MeshUVAnalyzer.CreateUVUsageFromMask(config.maskTexture, config.maskThreshold);
                }

                Texture2D analysisTexture = referenceTexture;

                if (uvUsage == null)
                {
                    if (config.referenceGameObject == null)
                        return Color.white;

                    analysisTexture = ResolveAnalysisTexture(config, referenceTexture);

                    // Analyze UV usage
                    uvUsage = MeshUVAnalyzer.AnalyzeGameObjectUVUsage(
                        config.referenceGameObject, analysisTexture, config.materialIndex, config.uvChannel);
                }

                if (uvUsage == null) return Color.white;

                // Convert normalized position to pixel coordinates
                int pixelX = Mathf.FloorToInt(normalizedPosition.x * referenceTexture.width);
                int pixelY = Mathf.FloorToInt((1f - normalizedPosition.y) * referenceTexture.height);
                
                pixelX = Mathf.Clamp(pixelX, 0, referenceTexture.width - 1);
                pixelY = Mathf.Clamp(pixelY, 0, referenceTexture.height - 1);
                
                int pixelIndex = pixelY * referenceTexture.width + pixelX;

                // Check if the selected pixel is in a used UV area
                if (pixelIndex < uvUsage.usedPixels.Length && uvUsage.usedPixels[pixelIndex])
                {
                    // Get the actual color from the texture
                    var readableTexture = TextureProcessor.MakeReadableCopy(referenceTexture);
                    if (readableTexture != null)
                    {
                        try
                        {
                            return readableTexture.GetPixel(pixelX, pixelY);
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(readableTexture);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("Selected pixel is not in a mesh-used UV area. Using dominant color instead.");
                    
                    // Return the most dominant color from used areas
                    var dominantColors = MeshUVAnalyzer.ExtractDominantColorsFromUsedAreas(
                        referenceTexture, uvUsage, 1);
                    
                    if (dominantColors.Count > 0)
                        return dominantColors[0];
                }

                return Color.white;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to extract high-precision target color: {e.Message}");
                return Color.white;
            }
        }

        private static Texture2D ResolveAnalysisTexture(HighPrecisionConfig config, Texture2D fallbackTexture)
        {
            if (config == null || config.referenceGameObject == null)
                return fallbackTexture;

            var renderer = config.referenceGameObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = config.referenceGameObject.GetComponentInChildren<Renderer>();
            }

            if (renderer == null)
                return fallbackTexture;

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                return fallbackTexture;

            int index = Mathf.Clamp(config.materialIndex, 0, materials.Length - 1);
            var material = materials[index];
            if (material == null)
                return fallbackTexture;

            var resolved = ResolveTextureFromMaterial(material, fallbackTexture);
            return resolved ?? fallbackTexture;
        }

        private static Texture2D ResolveTextureFromMaterial(Material material, Texture2D fallbackTexture)
        {
            if (material == null)
                return fallbackTexture;

            var mainTex = material.GetTexture("_MainTex") as Texture2D;
            if (mainTex != null)
                return mainTex;

            var shader = material.shader;
            if (shader == null)
                return fallbackTexture;

#if UNITY_EDITOR
            int propertyCount = ShaderUtil.GetPropertyCount(shader);
#else
            int propertyCount = shader.GetPropertyCount();
#endif

            Texture2D fallbackCandidate = null;

            for (int i = 0; i < propertyCount; i++)
            {
#if UNITY_EDITOR
                if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                    continue;

                string propName = ShaderUtil.GetPropertyName(shader, i);
#else
                if (shader.GetPropertyType(i) != ShaderPropertyType.Texture)
                    continue;

                string propName = shader.GetPropertyName(i);
#endif
                var candidate = material.GetTexture(propName) as Texture2D;
                if (candidate == null)
                    continue;

                if (fallbackTexture == null)
                    return candidate;

                if (candidate == fallbackTexture)
                    return candidate;

                if (!string.IsNullOrEmpty(fallbackTexture.name) &&
                    candidate.name == fallbackTexture.name &&
                    candidate.width == fallbackTexture.width &&
                    candidate.height == fallbackTexture.height)
                {
                    return candidate;
                }

                if (fallbackCandidate == null)
                {
                    fallbackCandidate = candidate;
                }
            }

            return fallbackCandidate ?? fallbackTexture;
        }

        // Validate high-precision configuration
        public static bool ValidateHighPrecisionConfig(HighPrecisionConfig config, Texture2D referenceTexture)
        {
            if (config == null)
            {
                Debug.LogError("High-precision config is null");
                return false;
            }

            if (referenceTexture == null)
            {
                Debug.LogError("Reference texture is required for high-precision mode");
                return false;
            }

            bool hasMask = config.maskTexture != null;

            if (hasMask)
            {
                if (config.maskTexture.width != referenceTexture.width || config.maskTexture.height != referenceTexture.height)
                {
                    Debug.LogError("Precomputed mask texture dimensions must match the reference texture.");
                    return false;
                }

                return true;
            }

            if (config.referenceGameObject == null)
            {
                Debug.LogError("Reference GameObject is required for high-precision mode when no mask is provided");
                return false;
            }

            // Check if GameObject has mesh components
            var meshRenderers = config.referenceGameObject.GetComponentsInChildren<MeshRenderer>();
            var skinnedMeshRenderers = config.referenceGameObject.GetComponentsInChildren<SkinnedMeshRenderer>();

            if (meshRenderers.Length == 0 && skinnedMeshRenderers.Length == 0)
            {
                Debug.LogError("Reference GameObject must have MeshRenderer or SkinnedMeshRenderer components");
                return false;
            }

            return true;
        }
    }
}