using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TexColAdjuster
{
    // TexColAdjuster's intelligent difference-based color transformation processor
    public static class DifferenceBasedProcessor
    {
        // Core algorithm: Extract color difference and apply to entire texture
        // TexColAdjuster's smart color matching approach
        public static Texture2D ProcessTexture(Texture2D sourceTexture, ColorPixel fromColor, ColorPixel toColor, 
            ColorTransformConfig config, BitArray selectionMask = null)
        {
            if (sourceTexture == null) return null;

            // Make texture readable
            var readableTexture = TextureProcessor.MakeTextureReadable(sourceTexture);
            if (readableTexture == null) return null;

            // Convert to ColorPixel array for efficient processing
            Color[] sourceColors = TextureUtils.GetPixelsSafe(readableTexture);
            if (sourceColors == null) return null;

            ColorPixel[] sourcePixels = ConvertToColorPixels(sourceColors);
            
            // Calculate the color difference (Color-Changer's core concept)
            ColorPixel colorDifference = fromColor.GetDifference(toColor);
            
            // Process pixels based on configuration
            ColorPixel[] processedPixels = ProcessPixels(sourcePixels, fromColor, colorDifference, config, selectionMask);
            
            // Convert back and create result texture
            Color[] resultColors = ConvertToColors(processedPixels);
            
            var resultTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
            if (TextureUtils.SetPixelsSafe(resultTexture, resultColors))
            {
                return resultTexture;
            }
            
            UnityEngine.Object.DestroyImmediate(resultTexture);
            return null;
        }

        // Process all pixels with difference-based transformation
        private static ColorPixel[] ProcessPixels(ColorPixel[] sourcePixels, ColorPixel fromColor, 
            ColorPixel colorDifference, ColorTransformConfig config, BitArray selectionMask)
        {
            ColorPixel[] result = new ColorPixel[sourcePixels.Length];
            
            for (int i = 0; i < sourcePixels.Length; i++)
            {
                // Check if pixel should be processed (selection mask)
                if (selectionMask != null && !selectionMask[i])
                {
                    result[i] = sourcePixels[i];
                    continue;
                }

                ColorPixel originalPixel = sourcePixels[i];
                ColorPixel processedPixel = originalPixel;

                // Apply difference-based transformation based on balance mode
                switch (config.balanceMode)
                {
                    case ColorTransformConfig.BalanceMode.Simple:
                        processedPixel = ProcessPixelSimple(originalPixel, colorDifference, config);
                        break;
                        
                    case ColorTransformConfig.BalanceMode.Weighted:
                        processedPixel = ProcessPixelWeighted(originalPixel, fromColor, colorDifference, config);
                        break;
                        
                    case ColorTransformConfig.BalanceMode.Advanced:
                        processedPixel = ProcessPixelAdvanced(originalPixel, fromColor, colorDifference, config);
                        break;
                }

                result[i] = processedPixel;
            }
            
            return result;
        }

        // Simple mode: Direct difference application
        private static ColorPixel ProcessPixelSimple(ColorPixel originalPixel, ColorPixel colorDifference, 
            ColorTransformConfig config)
        {
            // Apply the color difference directly with intensity
            ColorPixel transformedPixel = originalPixel.ApplyDifference(colorDifference, config.intensity);
            
            // Apply additional adjustments
            transformedPixel = ApplyAdditionalAdjustments(transformedPixel, config);
            
            return transformedPixel;
        }

        // Weighted mode: Distance-based weighting with intelligent falloff
        private static ColorPixel ProcessPixelWeighted(ColorPixel originalPixel, ColorPixel fromColor, 
            ColorPixel colorDifference, ColorTransformConfig config)
        {
            // Calculate similarity to the source color
            float distance = originalPixel.DistanceTo(fromColor);
            float maxDistance = Mathf.Sqrt(3f); // Maximum RGB distance
            float similarity = Mathf.Max(0f, 1f - (distance / maxDistance));
            
            // Apply radius-based adjustment
            float radiusInfluence = Mathf.Pow(similarity, 1f / config.selectionRadius);
            
            // Calculate transformation strength
            float transformStrength = Mathf.Max(config.minSimilarity, radiusInfluence) * config.intensity;
            
            // Apply weighted transformation
            ColorPixel transformedPixel = originalPixel.ApplyDifference(colorDifference, transformStrength);
            
            // Apply additional adjustments
            transformedPixel = ApplyAdditionalAdjustments(transformedPixel, config);
            
            // Blend based on transformation strength for smooth transitions
            return originalPixel.Blend(transformedPixel, transformStrength);
        }

        // Advanced mode: Gradient and area-based processing with adaptive algorithms
        private static ColorPixel ProcessPixelAdvanced(ColorPixel originalPixel, ColorPixel fromColor, 
            ColorPixel colorDifference, ColorTransformConfig config)
        {
            // Calculate similarity with advanced curve
            float distance = originalPixel.DistanceTo(fromColor);
            float normalizedDistance = distance / Mathf.Sqrt(3f);
            
            // Apply advanced similarity curve
            float similarity = Mathf.Pow(Mathf.Max(0f, 1f - normalizedDistance), 2f);
            
            // Multi-factor transformation strength calculation
            float baseStrength = similarity * config.intensity;
            float radiusAdjustedStrength = Mathf.Pow(baseStrength, 1f / config.selectionRadius);
            float finalStrength = Mathf.Max(config.minSimilarity, radiusAdjustedStrength);
            
            // Apply gradient-based color difference scaling
            ColorPixel scaledDifference = ScaleColorDifference(colorDifference, similarity, config);
            
            // Apply transformation
            ColorPixel transformedPixel = originalPixel.ApplyDifference(scaledDifference, finalStrength);
            
            // Apply additional adjustments
            transformedPixel = ApplyAdditionalAdjustments(transformedPixel, config);
            
            // Advanced blending with area inclusion/exclusion consideration
            float blendFactor = CalculateAdvancedBlendFactor(similarity, finalStrength, config);
            
            return originalPixel.Blend(transformedPixel, blendFactor);
        }

        // Scale color difference based on similarity (advanced mode feature)
        private static ColorPixel ScaleColorDifference(ColorPixel colorDifference, float similarity, 
            ColorTransformConfig config)
        {
            // Scale the difference based on similarity for more natural transitions
            float scaleFactor = Mathf.Lerp(0.5f, 1.5f, similarity);
            
            return new ColorPixel(
                (byte)(colorDifference.R * scaleFactor),
                (byte)(colorDifference.G * scaleFactor),
                (byte)(colorDifference.B * scaleFactor),
                colorDifference.A
            );
        }

        // Calculate advanced blend factor for smooth transitions
        private static float CalculateAdvancedBlendFactor(float similarity, float transformStrength, 
            ColorTransformConfig config)
        {
            // Multi-curve blending for natural results
            float primaryBlend = transformStrength;
            float secondaryBlend = Mathf.Pow(similarity, config.selectionRadius);
            
            // Combine factors with bias toward transformation strength
            return Mathf.Lerp(primaryBlend, secondaryBlend, 0.3f);
        }

        // Apply additional adjustments (brightness, contrast, gamma)
        private static ColorPixel ApplyAdditionalAdjustments(ColorPixel pixel, ColorTransformConfig config)
        {
            ColorPixel adjustedPixel = pixel;
            
            // Apply brightness and contrast
            if (config.brightness != 1.0f || config.contrast != 1.0f)
            {
                adjustedPixel = adjustedPixel.ApplyBrightnessContrast(config.brightness, config.contrast);
            }
            
            // Apply gamma correction
            if (config.gamma != 1.0f)
            {
                adjustedPixel = adjustedPixel.ApplyGamma(config.gamma);
            }
            
            // Apply transparency
            if (config.transparency > 0f)
            {
                adjustedPixel = new ColorPixel(
                    adjustedPixel.R,
                    adjustedPixel.G,
                    adjustedPixel.B,
                    (byte)(adjustedPixel.A * (1f - config.transparency))
                );
            }
            
            return adjustedPixel;
        }

        // Efficient conversion methods
        private static ColorPixel[] ConvertToColorPixels(Color[] colors)
        {
            ColorPixel[] pixels = new ColorPixel[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                pixels[i] = new ColorPixel(colors[i]);
            }
            return pixels;
        }

        private static Color[] ConvertToColors(ColorPixel[] pixels)
        {
            Color[] colors = new Color[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                colors[i] = pixels[i].ToColor();
            }
            return colors;
        }

        // Create selection mask using TexColAdjuster's intelligent flood-fill algorithm
        public static BitArray CreateFloodFillSelection(Texture2D texture, int startX, int startY, 
            float tolerance = 0.1f, int maxIterations = 10000)
        {
            if (texture == null) return null;
            
            Color[] pixels = TextureUtils.GetPixelsSafe(texture);
            if (pixels == null) return null;
            
            int width = texture.width;
            int height = texture.height;
            BitArray selection = new BitArray(pixels.Length, false);
            
            // Convert to ColorPixel for efficient processing
            ColorPixel[] colorPixels = ConvertToColorPixels(pixels);
            
            // Get starting pixel color
            int startIndex = startY * width + startX;
            if (startIndex < 0 || startIndex >= colorPixels.Length) return selection;
            
            ColorPixel targetColor = colorPixels[startIndex];
            
            // Flood-fill using stack-based approach (prevents stack overflow)
            Stack<Vector2Int> pixelsToProcess = new Stack<Vector2Int>();
            pixelsToProcess.Push(new Vector2Int(startX, startY));
            
            int iterations = 0;
            
            while (pixelsToProcess.Count > 0 && iterations < maxIterations)
            {
                Vector2Int currentPos = pixelsToProcess.Pop();
                int x = currentPos.x;
                int y = currentPos.y;
                
                // Check bounds
                if (x < 0 || x >= width || y < 0 || y >= height) continue;
                
                int index = y * width + x;
                
                // Skip if already processed
                if (selection[index]) continue;
                
                // Check color similarity
                float distance = colorPixels[index].DistanceTo(targetColor);
                if (distance > tolerance) continue;
                
                // Mark as selected
                selection[index] = true;
                
                // Add neighboring pixels
                pixelsToProcess.Push(new Vector2Int(x + 1, y));
                pixelsToProcess.Push(new Vector2Int(x - 1, y));
                pixelsToProcess.Push(new Vector2Int(x, y + 1));
                pixelsToProcess.Push(new Vector2Int(x, y - 1));
                
                iterations++;
            }
            
            return selection;
        }

        // Generate TexColAdjuster preview with selection outline
        public static Texture2D GeneratePreviewWithOutline(Texture2D sourceTexture, BitArray selectionMask, 
            Color outlineColor, int outlineWidth = 1)
        {
            if (sourceTexture == null || selectionMask == null) return null;
            
            Color[] sourcePixels = TextureUtils.GetPixelsSafe(sourceTexture);
            if (sourcePixels == null) return null;
            
            Color[] previewPixels = new Color[sourcePixels.Length];
            Array.Copy(sourcePixels, previewPixels, sourcePixels.Length);
            
            int width = sourceTexture.width;
            int height = sourceTexture.height;
            
            // Draw outline around selected areas
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    
                    if (selectionMask[index])
                    {
                        // Check if this pixel is on the border of selection
                        bool isBorder = false;
                        
                        for (int dy = -outlineWidth; dy <= outlineWidth && !isBorder; dy++)
                        {
                            for (int dx = -outlineWidth; dx <= outlineWidth && !isBorder; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                
                                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                                {
                                    int neighborIndex = ny * width + nx;
                                    if (!selectionMask[neighborIndex])
                                    {
                                        isBorder = true;
                                    }
                                }
                                else
                                {
                                    isBorder = true; // Edge of image
                                }
                            }
                        }
                        
                        if (isBorder)
                        {
                            previewPixels[index] = Color.Lerp(previewPixels[index], outlineColor, 0.7f);
                        }
                    }
                }
            }
            
            var previewTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            if (TextureUtils.SetPixelsSafe(previewTexture, previewPixels))
            {
                return previewTexture;
            }
            
            UnityEngine.Object.DestroyImmediate(previewTexture);
            return null;
        }
    }
}