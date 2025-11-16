using System;
using System.Collections;
using UnityEngine;

namespace TexColAdjuster
{
    // TexColAdjuster's high-performance optimization system
    public static class PerformanceOptimizedProcessor
    {
        // Cached preview for incremental updates
        private static CachedPreviewData cachedPreview = null;
        
        // TexColAdjuster's intelligent incremental processing for real-time preview
        public static Texture2D ProcessIncremental(Texture2D sourceTexture, ColorPixel fromColor, ColorPixel toColor, 
            ColorTransformConfig config, BitArray selectionMask = null, bool forceFullProcess = false)
        {
            // Check if we can use cached preview
            if (!forceFullProcess && CanUseCachedPreview(sourceTexture, fromColor, toColor, config))
            {
                return UpdateCachedPreview(config);
            }
            
            // Full processing required
            var result = ProcessWithCaching(sourceTexture, fromColor, toColor, config, selectionMask);
            
            // Update cache
            UpdatePreviewCache(sourceTexture, fromColor, toColor, config, result);
            
            return result;
        }

        // Process with performance optimizations
        private static Texture2D ProcessWithCaching(Texture2D sourceTexture, ColorPixel fromColor, ColorPixel toColor, 
            ColorTransformConfig config, BitArray selectionMask)
        {
            // Use reduced resolution for preview performance
            int maxPreviewSize = GetOptimalPreviewSize(sourceTexture);
            bool needsResize = sourceTexture.width > maxPreviewSize || sourceTexture.height > maxPreviewSize;
            
            Texture2D workingTexture = sourceTexture;
            BitArray workingMask = selectionMask;
            
            if (needsResize)
            {
                // Resize for preview performance
                var previewSize = CalculatePreviewSize(sourceTexture.width, sourceTexture.height, maxPreviewSize);
                workingTexture = ResizeTextureOptimized(sourceTexture, previewSize.x, previewSize.y);
                
                // Resize selection mask if present
                if (selectionMask != null)
                {
                    workingMask = ResizeSelectionMask(selectionMask, sourceTexture.width, sourceTexture.height, 
                        previewSize.x, previewSize.y);
                }
            }
            
            // Process with optimized algorithm
            var result = ProcessTextureOptimized(workingTexture, fromColor, toColor, config, workingMask);
            
            // Clean up temporary texture
            if (needsResize && workingTexture != sourceTexture)
            {
                UnityEngine.Object.DestroyImmediate(workingTexture);
            }
            
            return result;
        }

        // Optimized texture processing with minimal allocations
        private static Texture2D ProcessTextureOptimized(Texture2D sourceTexture, ColorPixel fromColor, ColorPixel toColor, 
            ColorTransformConfig config, BitArray selectionMask)
        {
            if (sourceTexture == null) return null;

            var readableTexture = TextureProcessor.MakeTextureReadable(sourceTexture);
            if (readableTexture == null) return null;

            Color[] sourceColors = TextureUtils.GetPixelsSafe(readableTexture);
            if (sourceColors == null) return null;

            // Pre-calculate transformation values for performance
            ColorPixel colorDifference = fromColor.GetDifference(toColor);
            TransformationCache transformCache = new TransformationCache(fromColor, colorDifference, config);
            
            // Process pixels with optimized loop
            Color[] resultColors = ProcessPixelsOptimized(sourceColors, transformCache, selectionMask);
            
            var resultTexture = TextureColorSpaceUtility.CreateRuntimeTextureLike(sourceTexture);
            if (TextureUtils.SetPixelsSafe(resultTexture, resultColors))
            {
                return resultTexture;
            }
            
            TextureColorSpaceUtility.UnregisterRuntimeTexture(resultTexture);
            UnityEngine.Object.DestroyImmediate(resultTexture);
            return null;
        }

        // Optimized pixel processing loop
        private static Color[] ProcessPixelsOptimized(Color[] sourceColors, TransformationCache cache, BitArray selectionMask)
        {
            Color[] result = new Color[sourceColors.Length];
            
            // Pre-calculate common values
            float intensityFactor = cache.config.intensity;
            float brightnessFactor = cache.config.brightness;
            float contrastFactor = cache.config.contrast;
            float gammaFactor = 1f / cache.config.gamma;
            
            for (int i = 0; i < sourceColors.Length; i++)
            {
                // Skip unselected pixels
                if (selectionMask != null && !selectionMask[i])
                {
                    result[i] = sourceColors[i];
                    continue;
                }

                // Convert to ColorPixel for efficient processing
                ColorPixel sourcePixel = new ColorPixel(sourceColors[i]);
                
                // Apply transformation based on balance mode
                ColorPixel transformedPixel = ApplyTransformationOptimized(sourcePixel, cache);
                
                // Convert back to Color
                result[i] = transformedPixel.ToColor();
            }
            
            return result;
        }

        // Optimized transformation application
        private static ColorPixel ApplyTransformationOptimized(ColorPixel sourcePixel, TransformationCache cache)
        {
            ColorPixel result = sourcePixel;
            
            switch (cache.config.balanceMode)
            {
                case ColorTransformConfig.BalanceMode.Simple:
                    result = sourcePixel.ApplyDifference(cache.colorDifference, cache.config.intensity);
                    break;
                    
                case ColorTransformConfig.BalanceMode.Weighted:
                    float distance = sourcePixel.DistanceTo(cache.fromColor);
                    float similarity = Mathf.Max(0f, 1f - (distance / 1.732f)); // sqrt(3)
                    float transformStrength = Mathf.Max(cache.config.minSimilarity, 
                        Mathf.Pow(similarity, 1f / cache.config.selectionRadius)) * cache.config.intensity;
                    
                    ColorPixel transformed = sourcePixel.ApplyDifference(cache.colorDifference, transformStrength);
                    result = sourcePixel.Blend(transformed, transformStrength);
                    break;
                    
                case ColorTransformConfig.BalanceMode.Advanced:
                    // Optimized advanced processing
                    result = ProcessAdvancedOptimized(sourcePixel, cache);
                    break;
            }
            
            // Apply additional adjustments if needed
            if (cache.hasAdjustments)
            {
                result = ApplyAdjustmentsOptimized(result, cache);
            }
            
            return result;
        }

        // Optimized advanced processing
        private static ColorPixel ProcessAdvancedOptimized(ColorPixel sourcePixel, TransformationCache cache)
        {
            float distance = sourcePixel.DistanceTo(cache.fromColor);
            float normalizedDistance = distance / 1.732f; // sqrt(3)
            float similarity = Mathf.Pow(Mathf.Max(0f, 1f - normalizedDistance), 2f);
            
            float baseStrength = similarity * cache.config.intensity;
            float radiusAdjustedStrength = Mathf.Pow(baseStrength, 1f / cache.config.selectionRadius);
            float finalStrength = Mathf.Max(cache.config.minSimilarity, radiusAdjustedStrength);
            
            ColorPixel transformedPixel = sourcePixel.ApplyDifference(cache.colorDifference, finalStrength);
            
            float blendFactor = Mathf.Lerp(finalStrength, Mathf.Pow(similarity, cache.config.selectionRadius), 0.3f);
            
            return sourcePixel.Blend(transformedPixel, blendFactor);
        }

        // Optimized adjustments application
        private static ColorPixel ApplyAdjustmentsOptimized(ColorPixel pixel, TransformationCache cache)
        {
            ColorPixel result = pixel;
            
            if (cache.config.brightness != 1.0f || cache.config.contrast != 1.0f)
            {
                result = result.ApplyBrightnessContrast(cache.config.brightness, cache.config.contrast);
            }
            
            if (cache.config.gamma != 1.0f)
            {
                result = result.ApplyGamma(cache.config.gamma);
            }
            
            if (cache.config.transparency > 0f)
            {
                result = new ColorPixel(result.R, result.G, result.B, 
                    (byte)(result.A * (1f - cache.config.transparency)));
            }
            
            return result;
        }

        // Efficient texture resizing for preview
        private static Texture2D ResizeTextureOptimized(Texture2D source, int newWidth, int newHeight)
        {
            Color[] sourcePixels = TextureUtils.GetPixelsSafe(source);
            if (sourcePixels == null) return null;
            
            Color[] resizedPixels = new Color[newWidth * newHeight];
            
            float xRatio = (float)source.width / newWidth;
            float yRatio = (float)source.height / newHeight;
            
            // Optimized nearest neighbor scaling
            for (int y = 0; y < newHeight; y++)
            {
                int sourceY = Mathf.FloorToInt(y * yRatio);
                sourceY = Mathf.Clamp(sourceY, 0, source.height - 1);
                
                for (int x = 0; x < newWidth; x++)
                {
                    int sourceX = Mathf.FloorToInt(x * xRatio);
                    sourceX = Mathf.Clamp(sourceX, 0, source.width - 1);
                    
                    resizedPixels[y * newWidth + x] = sourcePixels[sourceY * source.width + sourceX];
                }
            }
            
            var resizedTexture = TextureColorSpaceUtility.CreateRuntimeTexture(newWidth, newHeight, TextureFormat.RGBA32, false, TextureColorSpaceUtility.IsTextureSRGB(source));
            if (TextureUtils.SetPixelsSafe(resizedTexture, resizedPixels))
            {
                return resizedTexture;
            }
            
            TextureColorSpaceUtility.UnregisterRuntimeTexture(resizedTexture);
            UnityEngine.Object.DestroyImmediate(resizedTexture);
            return null;
        }

        // Resize selection mask to match texture size
        private static BitArray ResizeSelectionMask(BitArray sourceMask, int sourceWidth, int sourceHeight, 
            int targetWidth, int targetHeight)
        {
            BitArray resizedMask = new BitArray(targetWidth * targetHeight, false);
            
            float xRatio = (float)sourceWidth / targetWidth;
            float yRatio = (float)sourceHeight / targetHeight;
            
            for (int y = 0; y < targetHeight; y++)
            {
                int sourceY = Mathf.FloorToInt(y * yRatio);
                sourceY = Mathf.Clamp(sourceY, 0, sourceHeight - 1);
                
                for (int x = 0; x < targetWidth; x++)
                {
                    int sourceX = Mathf.FloorToInt(x * xRatio);
                    sourceX = Mathf.Clamp(sourceX, 0, sourceWidth - 1);
                    
                    int sourceIndex = sourceY * sourceWidth + sourceX;
                    int targetIndex = y * targetWidth + x;
                    
                    resizedMask[targetIndex] = sourceMask[sourceIndex];
                }
            }
            
            return resizedMask;
        }

        // Calculate optimal preview size based on system performance
        private static int GetOptimalPreviewSize(Texture2D texture)
        {
            // Dynamic sizing based on texture size and system capability
            int totalPixels = texture.width * texture.height;
            
            if (totalPixels > 2048 * 2048) return 512;
            if (totalPixels > 1024 * 1024) return 768;
            if (totalPixels > 512 * 512) return 1024;
            
            return 1024; // Default preview size
        }

        private static Vector2Int CalculatePreviewSize(int width, int height, int maxSize)
        {
            if (width <= maxSize && height <= maxSize)
                return new Vector2Int(width, height);
                
            float aspectRatio = (float)width / height;
            
            if (width > height)
            {
                return new Vector2Int(maxSize, Mathf.RoundToInt(maxSize / aspectRatio));
            }
            else
            {
                return new Vector2Int(Mathf.RoundToInt(maxSize * aspectRatio), maxSize);
            }
        }

        // Cache management for incremental updates
        private static bool CanUseCachedPreview(Texture2D sourceTexture, ColorPixel fromColor, ColorPixel toColor, 
            ColorTransformConfig config)
        {
            if (cachedPreview == null) return false;
            if (cachedPreview.sourceTexture != sourceTexture) return false;
            
            // Check if only minor adjustments changed (brightness, contrast, gamma)
            return cachedPreview.fromColor.Equals(fromColor) && 
                   cachedPreview.toColor.Equals(toColor) &&
                   cachedPreview.config.balanceMode == config.balanceMode &&
                   Mathf.Abs(cachedPreview.config.intensity - config.intensity) < 0.01f;
        }

        private static Texture2D UpdateCachedPreview(ColorTransformConfig config)
        {
            // Apply only adjustment changes to cached result
            if (cachedPreview?.cachedResult == null) return null;
            
            // For minor adjustments, we can apply them post-process
            // This is much faster than full re-processing
            return ApplyMinorAdjustmentsToTexture(cachedPreview.cachedResult, 
                cachedPreview.config, config);
        }

        private static Texture2D ApplyMinorAdjustmentsToTexture(Texture2D baseTexture, 
            ColorTransformConfig oldConfig, ColorTransformConfig newConfig)
        {
            Color[] pixels = TextureUtils.GetPixelsSafe(baseTexture);
            if (pixels == null) return null;
            
            // Calculate adjustment ratios
            float brightnessRatio = newConfig.brightness / oldConfig.brightness;
            float contrastRatio = newConfig.contrast / oldConfig.contrast;
            float gammaChange = newConfig.gamma / oldConfig.gamma;
            
            // Apply minor adjustments
            for (int i = 0; i < pixels.Length; i++)
            {
                ColorPixel pixel = new ColorPixel(pixels[i]);
                
                if (brightnessRatio != 1f || contrastRatio != 1f)
                {
                    pixel = pixel.ApplyBrightnessContrast(brightnessRatio, contrastRatio);
                }
                
                if (gammaChange != 1f)
                {
                    pixel = pixel.ApplyGamma(gammaChange);
                }
                
                pixels[i] = pixel.ToColor();
            }
            
            var result = TextureColorSpaceUtility.CreateRuntimeTextureLike(baseTexture);
            if (TextureUtils.SetPixelsSafe(result, pixels))
            {
                return result;
            }
            
            TextureColorSpaceUtility.UnregisterRuntimeTexture(result);
            UnityEngine.Object.DestroyImmediate(result);
            return null;
        }

        private static void UpdatePreviewCache(Texture2D sourceTexture, ColorPixel fromColor, ColorPixel toColor, 
            ColorTransformConfig config, Texture2D result)
        {
            // Clean up old cache
            if (cachedPreview?.cachedResult != null)
            {
                UnityEngine.Object.DestroyImmediate(cachedPreview.cachedResult);
            }
            
            // Create new cache
            cachedPreview = new CachedPreviewData
            {
                sourceTexture = sourceTexture,
                fromColor = fromColor,
                toColor = toColor,
                config = new ColorTransformConfig
                {
                    brightness = config.brightness,
                    contrast = config.contrast,
                    gamma = config.gamma,
                    transparency = config.transparency,
                    intensity = config.intensity,
                    balanceMode = config.balanceMode,
                    selectionRadius = config.selectionRadius,
                    minSimilarity = config.minSimilarity
                },
                cachedResult = result
            };
        }
    }

    // TexColAdjuster cache structure for performance optimization
    public class CachedPreviewData
    {
        public Texture2D sourceTexture;
        public ColorPixel fromColor;
        public ColorPixel toColor;
        public ColorTransformConfig config;
        public Texture2D cachedResult;
    }

    // TexColAdjuster pre-calculated transformation values for performance
    public struct TransformationCache
    {
        public ColorPixel fromColor;
        public ColorPixel colorDifference;
        public ColorTransformConfig config;
        public bool hasAdjustments;
        
        public TransformationCache(ColorPixel fromColor, ColorPixel colorDifference, ColorTransformConfig config)
        {
            this.fromColor = fromColor;
            this.colorDifference = colorDifference;
            this.config = config;
            this.hasAdjustments = config.brightness != 1f || config.contrast != 1f || 
                                config.gamma != 1f || config.transparency > 0f;
        }
    }
}