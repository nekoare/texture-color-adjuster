using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

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
            public int uvChannel = 0;
            
            
            [Header("Masking Settings")]
            public bool showVisualMask = true;
            public Color maskColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            [Range(0f, 1f)]
            public float maskIntensity = 0.3f;
            
            [Header("Color Extraction")]
            [Range(3, 10)]
            public int dominantColorCount = 5;
            public bool useWeightedSampling = true;
        }

        // Process color adjustment using high-precision mode
        public static Texture2D ProcessWithHighPrecision(Texture2D targetTexture, Texture2D referenceTexture,
            HighPrecisionConfig config, float intensity, bool preserveLuminance, ColorAdjustmentMode mode)
        {
            if (targetTexture == null || referenceTexture == null || config?.referenceGameObject == null)
                return null;

            try
            {
                // Analyze UV usage of the reference GameObject
                var uvUsage = MeshUVAnalyzer.AnalyzeGameObjectUVUsage(
                    config.referenceGameObject, referenceTexture, config.materialIndex, config.uvChannel);

                if (uvUsage == null)
                {
                    Debug.LogError("Failed to analyze UV usage for high-precision mode");
                    return null;
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
                return ProcessWithSyntheticReference(targetTexture, syntheticReference, 
                    intensity, preserveLuminance, mode);
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
            var readableTexture = TextureProcessor.MakeTextureReadable(referenceTexture);
            if (readableTexture == null) return null;

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

            var resultTexture = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.RGBA32, false);
            if (TextureUtils.SetPixelsSafe(resultTexture, adjustedPixels))
            {
                return resultTexture;
            }

            UnityEngine.Object.DestroyImmediate(resultTexture);
            return null;
        }

        // Create preview texture with visual mask overlay
        public static Texture2D CreateHighPrecisionPreview(Texture2D referenceTexture, 
            HighPrecisionConfig config, bool showMask = true)
        {
            if (referenceTexture == null || config?.referenceGameObject == null)
                return null;

            try
            {
                // Analyze UV usage
                var uvUsage = MeshUVAnalyzer.AnalyzeGameObjectUVUsage(
                    config.referenceGameObject, referenceTexture, config.materialIndex, config.uvChannel);

                if (uvUsage == null) return null;

                if (showMask && config.showVisualMask)
                {
                    // Create masked texture for preview
                    return MeshUVAnalyzer.CreateMaskedTexture(referenceTexture, uvUsage, 
                        config.maskColor, config.maskIntensity);
                }
                else
                {
                    // Return original texture
                    return referenceTexture;
                }
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
            if (referenceTexture == null || config?.referenceGameObject == null)
                return "No data available";

            try
            {
                var uvUsage = MeshUVAnalyzer.AnalyzeGameObjectUVUsage(
                    config.referenceGameObject, referenceTexture, config.materialIndex, config.uvChannel);

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
            if (referenceTexture == null || config?.referenceGameObject == null)
                return Color.white;

            try
            {
                // Analyze UV usage
                var uvUsage = MeshUVAnalyzer.AnalyzeGameObjectUVUsage(
                    config.referenceGameObject, referenceTexture, config.materialIndex, config.uvChannel);

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
                    var readableTexture = TextureProcessor.MakeTextureReadable(referenceTexture);
                    if (readableTexture != null)
                    {
                        return readableTexture.GetPixel(pixelX, pixelY);
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

        // Validate high-precision configuration
        public static bool ValidateHighPrecisionConfig(HighPrecisionConfig config, Texture2D referenceTexture)
        {
            if (config == null)
            {
                Debug.LogError("High-precision config is null");
                return false;
            }

            if (config.referenceGameObject == null)
            {
                Debug.LogError("Reference GameObject is required for high-precision mode");
                return false;
            }

            if (referenceTexture == null)
            {
                Debug.LogError("Reference texture is required for high-precision mode");
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