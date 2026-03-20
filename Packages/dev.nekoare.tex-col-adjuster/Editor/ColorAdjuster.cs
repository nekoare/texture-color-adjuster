using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TexColAdjuster;
using TexColAdjuster.Runtime;

namespace TexColAdjuster.Editor
{
    public static class ColorAdjuster
    {
        // Alpha threshold for considering a pixel transparent
        public const float ALPHA_THRESHOLD = 0.01f;
        public static Texture2D AdjustColors(Texture2D targetTexture, Texture2D referenceTexture, 
            float intensity, bool preserveLuminance, ColorAdjustmentMode mode)
        {
            if (targetTexture == null || referenceTexture == null)
                return null;
            
            Color[] targetPixels = TextureUtils.GetPixelsSafe(targetTexture);
            Color[] referencePixels = TextureUtils.GetPixelsSafe(referenceTexture);
            
            if (targetPixels == null || referencePixels == null)
                return null;
            
            Color[] adjustedPixels = new Color[targetPixels.Length];
            
            switch (mode)
            {
                case ColorAdjustmentMode.LabHistogramMatching:
                    adjustedPixels = LabHistogramMatching(targetPixels, referencePixels, intensity, preserveLuminance);
                    break;
                case ColorAdjustmentMode.HueShift:
                    adjustedPixels = HueShiftAdjustment(targetPixels, referencePixels, intensity, preserveLuminance);
                    break;
                case ColorAdjustmentMode.ColorTransfer:
                    adjustedPixels = ColorTransferAdjustment(targetPixels, referencePixels, intensity, preserveLuminance);
                    break;
                case ColorAdjustmentMode.AdaptiveAdjustment:
                    adjustedPixels = AdaptiveAdjustment(targetPixels, referencePixels, intensity, preserveLuminance);
                    break;
                default:
                    adjustedPixels = targetPixels;
                    break;
            }
            
            // Transparency is now handled within each adjustment method (no separate pass needed)

            var result = TextureColorSpaceUtility.CreateRuntimeTextureLike(targetTexture);
            if (TextureUtils.SetPixelsSafe(result, adjustedPixels))
            {
                return result;
            }

            TextureColorSpaceUtility.UnregisterRuntimeTexture(result);
            UnityEngine.Object.DestroyImmediate(result);
            return null;
        }
        
        public static Texture2D AdjustColorsWithDualSelection(Texture2D targetTexture, Texture2D referenceTexture, 
            Color targetColor, Color referenceColor, float intensity, bool preserveLuminance, ColorAdjustmentMode mode, float selectionRange = 0.3f)
        {
            if (targetTexture == null || referenceTexture == null)
                return null;

            // Convert UI colors (gamma/sRGB space) to linear for LAB processing
            targetColor = targetColor.linear;
            referenceColor = referenceColor.linear;

            Color[] targetPixels = TextureUtils.GetPixelsSafe(targetTexture);
            Color[] referencePixels = TextureUtils.GetPixelsSafe(referenceTexture);

            if (targetPixels == null || referencePixels == null)
                return null;

            // Direct color matching approach for precise local color alignment
            // This prioritizes making the selected colors match exactly
            Color[] adjustedPixels = DirectColorMatching(targetPixels, targetColor, referenceColor, intensity, preserveLuminance, selectionRange);
            
            var result = TextureColorSpaceUtility.CreateRuntimeTextureLike(targetTexture);
            if (TextureUtils.SetPixelsSafe(result, adjustedPixels))
            {
                return result;
            }

            TextureColorSpaceUtility.UnregisterRuntimeTexture(result);
            UnityEngine.Object.DestroyImmediate(result);
            return null;
        }
        
        /// <summary>
        /// LABマッチング済みテクスチャに対して、選択した2色の周辺色域をさらに参照色方向に補正する。
        /// 全体の色合わせはLABマッチングで済んでいる前提で、局所的な色の一致度を高める。
        /// </summary>
        public static Texture2D ApplyDualSelectionRefinement(Texture2D labMatchedTexture,
            Color targetColor, Color referenceColor, float selectionRange = 0.3f)
        {
            if (labMatchedTexture == null) return null;

            Color[] pixels = TextureUtils.GetPixelsSafe(labMatchedTexture);
            if (pixels == null) return null;

            // Convert UI colors to linear for LAB processing
            Color linearTarget = targetColor.linear;
            Color linearRef = referenceColor.linear;

            Color[] refined = DirectColorMatching(pixels, linearTarget, linearRef, 1f, false, selectionRange);

            var result = TextureColorSpaceUtility.CreateRuntimeTextureLike(labMatchedTexture);
            if (TextureUtils.SetPixelsSafe(result, refined))
            {
                return result;
            }

            TextureColorSpaceUtility.UnregisterRuntimeTexture(result);
            UnityEngine.Object.DestroyImmediate(result);
            return null;
        }

        // Direct color matching for precise local color alignment
        // This approach transforms the entire texture based on color space distance while preserving texture variation
        private static Color[] DirectColorMatching(Color[] targetPixels, Color targetColor, Color referenceColor,
            float intensity, bool preserveLuminance, float selectionRange)
        {
            Color[] adjustedPixels = new Color[targetPixels.Length];
            
            // Convert selected colors to LAB space for better color matching
            Vector3 targetLab = ColorSpaceConverter.RGBtoLAB(targetColor);
            Vector3 referenceLab = ColorSpaceConverter.RGBtoLAB(referenceColor);
            
            // Calculate the color transformation vector
            Vector3 colorTransform = new Vector3(
                referenceLab.x - targetLab.x, // Lightness shift
                referenceLab.y - targetLab.y, // A channel shift
                referenceLab.z - targetLab.z  // B channel shift
            );
            
            for (int i = 0; i < targetPixels.Length; i++)
            {
                Color originalPixel = targetPixels[i];
                Vector3 originalLab = ColorSpaceConverter.RGBtoLAB(originalPixel);
                
                // Calculate color space distance from the selected target color
                float colorDistance = CalculateLabDistance(originalLab, targetLab);
                
                // Calculate transformation strength based on color space distance
                // Closer colors get stronger transformation, but all colors are transformed
                float transformationStrength = CalculateTransformationStrength(colorDistance, selectionRange, intensity);
                
                // Apply adaptive color transformation that preserves texture variation
                Vector3 adjustedLab = ApplyAdaptiveColorTransformation(
                    originalLab, targetLab, referenceLab, colorTransform, transformationStrength
                );
                
                // Handle luminance preservation
                if (preserveLuminance)
                {
                    // Preserve original lightness but allow color transformation
                    adjustedLab.x = Mathf.Lerp(adjustedLab.x, originalLab.x, 0.7f);
                }
                
                // Convert back to RGB (preserve original alpha)
                Color adjustedColor = ColorSpaceConverter.LABtoRGB(adjustedLab, originalPixel.a);

                // Final blending to ensure smooth transitions
                adjustedPixels[i] = Color.Lerp(originalPixel, adjustedColor, transformationStrength);
            }
            
            return adjustedPixels;
        }
        
        // Calculate LAB color space distance
        private static float CalculateLabDistance(Vector3 lab1, Vector3 lab2)
        {
            float lDiff = lab1.x - lab2.x;
            float aDiff = lab1.y - lab2.y;
            float bDiff = lab1.z - lab2.z;
            
            // Calculate Delta E (CIE76) - perceptual color difference
            return Mathf.Sqrt(lDiff * lDiff + aDiff * aDiff + bDiff * bDiff);
        }
        
        // Calculate transformation strength based on color space distance
        private static float CalculateTransformationStrength(float colorDistance, float selectionRange, float intensity)
        {
            // Define maximum meaningful color distance in LAB space
            float maxDistance = 100f;
            
            // Normalize distance to 0-1 range
            float normalizedDistance = Mathf.Clamp01(colorDistance / maxDistance);
            
            // Calculate base transformation strength (inverse of distance)
            float baseStrength = 1.0f - normalizedDistance;
            
            // Apply selection range influence
            float rangeInfluence = Mathf.Lerp(0.1f, 1.0f, selectionRange);
            
            // Calculate final transformation strength
            // Even distant colors get some transformation, but much weaker
            float transformationStrength = Mathf.Lerp(
                baseStrength * 0.1f, // Minimum transformation for distant colors
                baseStrength * rangeInfluence, // Full transformation for close colors
                baseStrength
            );
            
            return transformationStrength * intensity;
        }
        
        // Apply adaptive color transformation that preserves texture variation
        private static Vector3 ApplyAdaptiveColorTransformation(
            Vector3 originalLab, Vector3 targetLab, Vector3 referenceLab, Vector3 colorTransform, float strength)
        {
            // Calculate the relative position of the original color from the target color
            Vector3 relativePosition = new Vector3(
                originalLab.x - targetLab.x,
                originalLab.y - targetLab.y,
                originalLab.z - targetLab.z
            );
            
            // Apply transformation while preserving relative texture variation
            Vector3 transformedLab = new Vector3(
                referenceLab.x + relativePosition.x, // Maintain lightness variation
                referenceLab.y + relativePosition.y, // Maintain A channel variation
                referenceLab.z + relativePosition.z  // Maintain B channel variation
            );
            
            // Blend between original and transformed based on strength
            return Vector3.Lerp(originalLab, transformedLab, strength);
        }
        
        // Create a virtual texture where pixels similar to sourceColor are replaced with targetColor
        // while preserving saturation and brightness variation ratios
        private static Color[] CreateVirtualTextureWithColorUnification(Color[] pixels, Color sourceColor, Color targetColor, float selectionRange)
        {
            Color[] virtualPixels = new Color[pixels.Length];
            
            // Convert base colors to LAB space for better color analysis
            Vector3 sourceLab = ColorSpaceConverter.RGBtoLAB(sourceColor);
            Vector3 targetLab = ColorSpaceConverter.RGBtoLAB(targetColor);
            
            for (int i = 0; i < pixels.Length; i++)
            {
                Color originalPixel = pixels[i];
                float similarity = CalculateColorSimilarity(originalPixel, sourceColor);
                
                if (similarity > (1.0f - selectionRange))
                {
                    // Convert original pixel to LAB space
                    Vector3 originalLab = ColorSpaceConverter.RGBtoLAB(originalPixel);
                    
                    // Calculate the relative difference from source color
                    Vector3 relativeDifference = new Vector3(
                        originalLab.x - sourceLab.x, // Lightness difference
                        originalLab.y - sourceLab.y, // A channel difference  
                        originalLab.z - sourceLab.z  // B channel difference
                    );
                    
                    // Apply the same relative difference to target color
                    // This preserves the texture's saturation and brightness variation
                    Vector3 adjustedLab = new Vector3(
                        targetLab.x + relativeDifference.x,
                        targetLab.y + relativeDifference.y,
                        targetLab.z + relativeDifference.z
                    );
                    
                    // Convert back to RGB (preserve original alpha)
                    Color adjustedColor = ColorSpaceConverter.LABtoRGB(adjustedLab, originalPixel.a);

                    // Blend based on similarity for smooth transitions
                    float blendFactor = similarity;
                    virtualPixels[i] = Color.Lerp(originalPixel, adjustedColor, blendFactor);
                }
                else
                {
                    // Keep non-matching pixels unchanged
                    virtualPixels[i] = originalPixel;
                }
            }
            
            return virtualPixels;
        }
        
        public static Texture2D AdjustColorsWithTargetMainColor(Texture2D targetTexture, Texture2D referenceTexture, Color targetMainColor,
            float intensity, bool preserveLuminance, ColorAdjustmentMode mode)
        {
            if (targetTexture == null || referenceTexture == null)
                return null;

            // Convert UI color (gamma/sRGB space) to linear for LAB processing
            targetMainColor = targetMainColor.linear;

            Color[] targetPixels = TextureUtils.GetPixelsSafe(targetTexture);
            Color[] referencePixels = TextureUtils.GetPixelsSafe(referenceTexture);
            
            if (targetPixels == null || referencePixels == null)
                return null;
            
            // Create synthetic reference pixels with target main color as dominant
            Color[] syntheticReference = CreateSyntheticReferenceWithMainColor(referencePixels, targetMainColor);
            
            Color[] adjustedPixels = new Color[targetPixels.Length];
            
            switch (mode)
            {
                case ColorAdjustmentMode.LabHistogramMatching:
                    adjustedPixels = LabHistogramMatching(targetPixels, syntheticReference, intensity, preserveLuminance);
                    break;
                case ColorAdjustmentMode.HueShift:
                    adjustedPixels = HueShiftAdjustment(targetPixels, syntheticReference, intensity, preserveLuminance);
                    break;
                case ColorAdjustmentMode.ColorTransfer:
                    adjustedPixels = ColorTransferAdjustment(targetPixels, syntheticReference, intensity, preserveLuminance);
                    break;
                case ColorAdjustmentMode.AdaptiveAdjustment:
                    adjustedPixels = AdaptiveAdjustment(targetPixels, syntheticReference, intensity, preserveLuminance);
                    break;
                default:
                    adjustedPixels = targetPixels;
                    break;
            }
            
            // Use RGBA32 format to ensure SetPixels compatibility
            var result = TextureColorSpaceUtility.CreateRuntimeTextureLike(targetTexture);
            if (TextureUtils.SetPixelsSafe(result, adjustedPixels))
            {
                return result;
            }

            TextureColorSpaceUtility.UnregisterRuntimeTexture(result);
            UnityEngine.Object.DestroyImmediate(result);
            return null;
        }
        
        // Color similarity for Dual Color Selection (more permissive)
        private static float CalculateColorSimilarity(Color color1, Color color2)
        {
            // Use simpler RGB distance for more intuitive color selection
            float rDiff = Mathf.Abs(color1.r - color2.r);
            float gDiff = Mathf.Abs(color1.g - color2.g);
            float bDiff = Mathf.Abs(color1.b - color2.b);
            
            // Calculate Euclidean distance in RGB space
            float distance = Mathf.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
            float maxDistance = Mathf.Sqrt(3f); // Maximum possible distance in RGB space
            
            // Convert to similarity (0-1 scale)
            float similarity = Mathf.Max(0f, 1f - (distance / maxDistance));
            
            // Apply moderate curve for better selection
            similarity = Mathf.Pow(similarity, 2.0f);
            
            return similarity;
        }
        
        // Original color similarity for traditional processing (more conservative)
        private static float CalculateColorSimilarityTraditional(Color color1, Color color2)
        {
            // Use LAB color space for traditional processing
            Vector3 lab1 = ColorSpaceConverter.RGBtoLAB(color1);
            Vector3 lab2 = ColorSpaceConverter.RGBtoLAB(color2);
            
            // Calculate Delta E (CIE76) - perceptual color difference
            float lDiff = lab1.x - lab2.x;
            float aDiff = lab1.y - lab2.y;
            float bDiff = lab1.z - lab2.z;
            
            float deltaE = Mathf.Sqrt(lDiff * lDiff + aDiff * aDiff + bDiff * bDiff);
            
            // Convert to similarity (0-1 scale)
            float maxDeltaE = 100f;
            float similarity = Mathf.Max(0f, 1f - (deltaE / maxDeltaE));
            
            // Apply conservative curve
            similarity = Mathf.Pow(similarity, 2.5f);
            
            return similarity;
        }
        
        private static Color[] LabHistogramMatching(Color[] targetPixels, Color[] referencePixels,
            float intensity, bool preserveLuminance)
        {
            // Calculate LAB statistics in a single pass (opaque pixels only, no LINQ allocation)
            var targetStats = CalculateLabStatisticsFromPixels(targetPixels);
            var referenceStats = CalculateLabStatisticsFromPixels(referencePixels);

            var adjustedPixels = new Color[targetPixels.Length];

            for (int i = 0; i < targetPixels.Length; i++)
            {
                Color pixel = targetPixels[i];

                // Skip transparent pixels
                if (pixel.a < ALPHA_THRESHOLD)
                {
                    adjustedPixels[i] = pixel;
                    continue;
                }

                // Convert to LAB inline (no pre-allocated array needed)
                Vector3 originalLab = ColorSpaceConverter.RGBtoLAB(pixel);

                // Histogram matching
                float adjustedL = MatchHistogram(originalLab.x, targetStats.lMean, targetStats.lStd,
                    referenceStats.lMean, referenceStats.lStd);
                float adjustedA = MatchHistogram(originalLab.y, targetStats.aMean, targetStats.aStd,
                    referenceStats.aMean, referenceStats.aStd);
                float adjustedB = MatchHistogram(originalLab.z, targetStats.bMean, targetStats.bStd,
                    referenceStats.bMean, referenceStats.bStd);

                // Apply intensity blending
                float lIntensity = preserveLuminance ? intensity * 0.5f : intensity;
                float finalL = Mathf.Lerp(originalLab.x, adjustedL, lIntensity);
                float finalA = Mathf.Lerp(originalLab.y, adjustedA, intensity);
                float finalB = Mathf.Lerp(originalLab.z, adjustedB, intensity);

                Vector3 adjustedLabFinal = new Vector3(finalL, finalA, finalB);

                // Convert back to RGB (preserve original alpha)
                Color adjustedColor = ColorSpaceConverter.LABtoRGB(adjustedLabFinal, pixel.a);

                // Conditional luminance preservation
                if (preserveLuminance)
                {
                    Color fullyPreserved = ColorSpaceConverter.PreserveLuminance(pixel, adjustedColor);
                    float preservationStrength = 1.0f - (intensity * 0.5f);
                    adjustedColor = Color.Lerp(adjustedColor, fullyPreserved, preservationStrength);
                }

                adjustedPixels[i] = adjustedColor;
            }

            return adjustedPixels;
        }

        // Single-pass LAB statistics calculation directly from Color[] (skips transparent pixels)
        private static LabStatistics CalculateLabStatisticsFromPixels(Color[] pixels)
        {
            var stats = new LabStatistics();
            if (pixels == null || pixels.Length == 0)
                return stats;

            // Pass 1: calculate means
            float lSum = 0f, aSum = 0f, bSum = 0f;
            int opaqueCount = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < ALPHA_THRESHOLD)
                    continue;

                Vector3 lab = ColorSpaceConverter.RGBtoLAB(pixels[i]);
                lSum += lab.x;
                aSum += lab.y;
                bSum += lab.z;
                opaqueCount++;
            }

            if (opaqueCount == 0)
                return stats;

            stats.lMean = lSum / opaqueCount;
            stats.aMean = aSum / opaqueCount;
            stats.bMean = bSum / opaqueCount;

            // Pass 2: calculate standard deviations
            float lVarSum = 0f, aVarSum = 0f, bVarSum = 0f;

            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < ALPHA_THRESHOLD)
                    continue;

                Vector3 lab = ColorSpaceConverter.RGBtoLAB(pixels[i]);
                float lDiff = lab.x - stats.lMean;
                float aDiff = lab.y - stats.aMean;
                float bDiff = lab.z - stats.bMean;
                lVarSum += lDiff * lDiff;
                aVarSum += aDiff * aDiff;
                bVarSum += bDiff * bDiff;
            }

            stats.lStd = Mathf.Sqrt(lVarSum / opaqueCount);
            stats.aStd = Mathf.Sqrt(aVarSum / opaqueCount);
            stats.bStd = Mathf.Sqrt(bVarSum / opaqueCount);

            return stats;
        }
        
        
        private static Color[] HueShiftAdjustment(Color[] targetPixels, Color[] referencePixels,
            float intensity, bool preserveLuminance)
        {
            // Calculate dominant hue (filtering transparent pixels inline)
            float dominantHue = CalculateDominantHueFiltered(referencePixels);
            float targetDominantHue = CalculateDominantHueFiltered(targetPixels);

            float hueShift = dominantHue - targetDominantHue;

            var adjustedPixels = new Color[targetPixels.Length];

            for (int i = 0; i < targetPixels.Length; i++)
            {
                // Skip transparent pixels
                if (targetPixels[i].a < ALPHA_THRESHOLD)
                {
                    adjustedPixels[i] = targetPixels[i];
                    continue;
                }
                
                Vector3 hsv = ColorSpaceConverter.RGBtoHSV(targetPixels[i]);
                
                // Apply hue shift
                hsv.x = (hsv.x + hueShift * intensity) % 360f;
                if (hsv.x < 0) hsv.x += 360f;
                
                Color adjustedColor = ColorSpaceConverter.HSVtoRGB(hsv, targetPixels[i].a);

                if (preserveLuminance)
                {
                    adjustedColor = ColorSpaceConverter.PreserveLuminance(targetPixels[i], adjustedColor);
                }

                adjustedPixels[i] = adjustedColor;
            }

            return adjustedPixels;
        }

        private static Color[] ColorTransferAdjustment(Color[] targetPixels, Color[] referencePixels,
            float intensity, bool preserveLuminance)
        {
            // Calculate color statistics (skipping transparent pixels inline)
            var targetStats = CalculateColorStatisticsFiltered(targetPixels);
            var referenceStats = CalculateColorStatisticsFiltered(referencePixels);

            var adjustedPixels = new Color[targetPixels.Length];

            for (int i = 0; i < targetPixels.Length; i++)
            {
                // Skip transparent pixels
                if (targetPixels[i].a < ALPHA_THRESHOLD)
                {
                    adjustedPixels[i] = targetPixels[i];
                    continue;
                }
                
                Color original = targetPixels[i];
                
                // Transfer color statistics
                float newR = TransferColorChannel(original.r, targetStats.rMean, targetStats.rStd, 
                    referenceStats.rMean, referenceStats.rStd);
                float newG = TransferColorChannel(original.g, targetStats.gMean, targetStats.gStd, 
                    referenceStats.gMean, referenceStats.gStd);
                float newB = TransferColorChannel(original.b, targetStats.bMean, targetStats.bStd, 
                    referenceStats.bMean, referenceStats.bStd);
                
                Color transferredColor = new Color(newR, newG, newB, original.a);
                
                // Blend with original
                Color adjustedColor = ColorSpaceConverter.BlendColors(original, transferredColor, intensity);
                
                if (preserveLuminance)
                {
                    adjustedColor = ColorSpaceConverter.PreserveLuminance(original, adjustedColor);
                }
                
                adjustedPixels[i] = adjustedColor;
            }
            
            return adjustedPixels;
        }
        
        private static Color[] AdaptiveAdjustment(Color[] targetPixels, Color[] referencePixels, 
            float intensity, bool preserveLuminance)
        {
            // This is a more sophisticated approach that adapts based on local color context
            var adjustedPixels = new Color[targetPixels.Length];
            
            // For simplicity, we'll use a combination of histogram matching and color transfer
            var histogramResult = LabHistogramMatching(targetPixels, referencePixels, intensity * 0.7f, preserveLuminance);
            var colorTransferResult = ColorTransferAdjustment(targetPixels, referencePixels, intensity * 0.3f, preserveLuminance);
            
            for (int i = 0; i < targetPixels.Length; i++)
            {
                // Skip transparent pixels
                if (targetPixels[i].a < ALPHA_THRESHOLD)
                {
                    adjustedPixels[i] = targetPixels[i];
                    continue;
                }

                adjustedPixels[i] = ColorSpaceConverter.BlendColors(histogramResult[i], colorTransferResult[i], 0.5f);
            }
            
            return adjustedPixels;
        }
        
        private static LabStatistics CalculateLabStatistics(Vector3[] labColors)
        {
            var stats = new LabStatistics();
            
            if (labColors.Length == 0)
                return stats;
            
            // Calculate means
            float lSum = 0, aSum = 0, bSum = 0;
            foreach (var lab in labColors)
            {
                lSum += lab.x;
                aSum += lab.y;
                bSum += lab.z;
            }
            
            stats.lMean = lSum / labColors.Length;
            stats.aMean = aSum / labColors.Length;
            stats.bMean = bSum / labColors.Length;
            
            // Calculate standard deviations
            float lVarSum = 0, aVarSum = 0, bVarSum = 0;
            foreach (var lab in labColors)
            {
                lVarSum += Mathf.Pow(lab.x - stats.lMean, 2);
                aVarSum += Mathf.Pow(lab.y - stats.aMean, 2);
                bVarSum += Mathf.Pow(lab.z - stats.bMean, 2);
            }
            
            stats.lStd = Mathf.Sqrt(lVarSum / labColors.Length);
            stats.aStd = Mathf.Sqrt(aVarSum / labColors.Length);
            stats.bStd = Mathf.Sqrt(bVarSum / labColors.Length);
            
            return stats;
        }
        
        private static ColorStatistics CalculateColorStatistics(Color[] colors)
        {
            var stats = new ColorStatistics();
            
            if (colors.Length == 0)
                return stats;
            
            // Calculate means
            float rSum = 0, gSum = 0, bSum = 0;
            foreach (var color in colors)
            {
                rSum += color.r;
                gSum += color.g;
                bSum += color.b;
            }
            
            stats.rMean = rSum / colors.Length;
            stats.gMean = gSum / colors.Length;
            stats.bMean = bSum / colors.Length;
            
            // Calculate standard deviations
            float rVarSum = 0, gVarSum = 0, bVarSum = 0;
            foreach (var color in colors)
            {
                rVarSum += Mathf.Pow(color.r - stats.rMean, 2);
                gVarSum += Mathf.Pow(color.g - stats.gMean, 2);
                bVarSum += Mathf.Pow(color.b - stats.bMean, 2);
            }
            
            stats.rStd = Mathf.Sqrt(rVarSum / colors.Length);
            stats.gStd = Mathf.Sqrt(gVarSum / colors.Length);
            stats.bStd = Mathf.Sqrt(bVarSum / colors.Length);

            return stats;
        }

        // Single-pass color statistics from Color[] skipping transparent pixels
        private static ColorStatistics CalculateColorStatisticsFiltered(Color[] pixels)
        {
            var stats = new ColorStatistics();
            if (pixels == null || pixels.Length == 0)
                return stats;

            float rSum = 0f, gSum = 0f, bSum = 0f;
            int opaqueCount = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < ALPHA_THRESHOLD)
                    continue;
                rSum += pixels[i].r;
                gSum += pixels[i].g;
                bSum += pixels[i].b;
                opaqueCount++;
            }

            if (opaqueCount == 0)
                return stats;

            stats.rMean = rSum / opaqueCount;
            stats.gMean = gSum / opaqueCount;
            stats.bMean = bSum / opaqueCount;

            float rVarSum = 0f, gVarSum = 0f, bVarSum = 0f;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < ALPHA_THRESHOLD)
                    continue;
                float rDiff = pixels[i].r - stats.rMean;
                float gDiff = pixels[i].g - stats.gMean;
                float bDiff = pixels[i].b - stats.bMean;
                rVarSum += rDiff * rDiff;
                gVarSum += gDiff * gDiff;
                bVarSum += bDiff * bDiff;
            }

            stats.rStd = Mathf.Sqrt(rVarSum / opaqueCount);
            stats.gStd = Mathf.Sqrt(gVarSum / opaqueCount);
            stats.bStd = Mathf.Sqrt(bVarSum / opaqueCount);

            return stats;
        }

        private const float HISTOGRAM_MIN_STD = 1.0f;
        private const float HISTOGRAM_MAX_SIGMA = 3.0f;

        private static float MatchHistogram(float value, float sourceMean, float sourceStd,
            float targetMean, float targetStd)
        {
            // Enforce minimum std to prevent noise amplification in low-variance areas (dark regions)
            float safeSourceStd = Mathf.Max(sourceStd, HISTOGRAM_MIN_STD);

            // Normalize to standard distribution and clamp to ±3σ (covers 99.7% of data)
            float normalized = (value - sourceMean) / safeSourceStd;
            normalized = Mathf.Clamp(normalized, -HISTOGRAM_MAX_SIGMA, HISTOGRAM_MAX_SIGMA);

            return normalized * targetStd + targetMean;
        }

        private static float TransferColorChannel(float value, float sourceMean, float sourceStd,
            float targetMean, float targetStd)
        {
            if (sourceStd == 0)
                return Mathf.Clamp01(value);
            
            float normalized = (value - sourceMean) / sourceStd;
            float transferred = normalized * targetStd + targetMean;
            
            return Mathf.Clamp01(transferred);
        }
        
        private static float CalculateDominantHue(Color[] colors)
        {
            if (colors.Length == 0)
                return 0f;

            // Circular mean using atan2(sin, cos) to correctly average hue angles
            float sinSum = 0f;
            float cosSum = 0f;
            int validHueCount = 0;

            foreach (var color in colors)
            {
                Vector3 hsv = ColorSpaceConverter.RGBtoHSV(color);
                if (hsv.y > 0.1f) // Only consider colors with sufficient saturation
                {
                    float hueRad = hsv.x * Mathf.Deg2Rad;
                    sinSum += Mathf.Sin(hueRad);
                    cosSum += Mathf.Cos(hueRad);
                    validHueCount++;
                }
            }

            if (validHueCount == 0)
                return 0f;

            float avgRad = Mathf.Atan2(sinSum / validHueCount, cosSum / validHueCount);
            float avgDeg = avgRad * Mathf.Rad2Deg;
            if (avgDeg < 0f) avgDeg += 360f;
            return avgDeg;
        }

        // CalculateDominantHue that skips transparent pixels inline
        private static float CalculateDominantHueFiltered(Color[] colors)
        {
            if (colors == null || colors.Length == 0)
                return 0f;

            float sinSum = 0f;
            float cosSum = 0f;
            int validHueCount = 0;

            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i].a < ALPHA_THRESHOLD)
                    continue;

                Vector3 hsv = ColorSpaceConverter.RGBtoHSV(colors[i]);
                if (hsv.y > 0.1f)
                {
                    float hueRad = hsv.x * Mathf.Deg2Rad;
                    sinSum += Mathf.Sin(hueRad);
                    cosSum += Mathf.Cos(hueRad);
                    validHueCount++;
                }
            }

            if (validHueCount == 0)
                return 0f;

            float avgRad = Mathf.Atan2(sinSum / validHueCount, cosSum / validHueCount);
            float avgDeg = avgRad * Mathf.Rad2Deg;
            if (avgDeg < 0f) avgDeg += 360f;
            return avgDeg;
        }

        public static List<Color> ExtractDominantColors(Texture2D texture, int colorCount = 5)
        {
            if (texture == null)
                return new List<Color>();
            
            Color[] pixels = TextureUtils.GetPixelsSafe(texture);
            if (pixels == null)
                return new List<Color>();
            
            // Use k-means clustering to find dominant colors
            return KMeansColorClustering(pixels, colorCount);
        }
        
        private static List<Color> KMeansColorClustering(Color[] pixels, int k)
        {
            if (pixels.Length == 0 || k <= 0)
                return new List<Color>();
            
            var random = new System.Random();
            var centroids = new List<Color>();
            
            // Initialize centroids randomly
            for (int i = 0; i < k; i++)
            {
                centroids.Add(pixels[random.Next(pixels.Length)]);
            }
            
            // Iterate until convergence
            for (int iteration = 0; iteration < 10; iteration++)
            {
                var clusters = new List<Color>[k];
                for (int i = 0; i < k; i++)
                {
                    clusters[i] = new List<Color>();
                }
                
                // Assign pixels to clusters
                foreach (var pixel in pixels)
                {
                    int nearestCentroid = 0;
                    float minDistance = ColorSpaceConverter.ColorDistanceRGB(pixel, centroids[0]);
                    
                    for (int i = 1; i < k; i++)
                    {
                        float distance = ColorSpaceConverter.ColorDistanceRGB(pixel, centroids[i]);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            nearestCentroid = i;
                        }
                    }
                    
                    clusters[nearestCentroid].Add(pixel);
                }
                
                // Update centroids
                for (int i = 0; i < k; i++)
                {
                    if (clusters[i].Count > 0)
                    {
                        float r = 0, g = 0, b = 0;
                        foreach (var color in clusters[i])
                        {
                            r += color.r;
                            g += color.g;
                            b += color.b;
                        }
                        
                        centroids[i] = new Color(
                            r / clusters[i].Count,
                            g / clusters[i].Count,
                            b / clusters[i].Count,
                            1.0f
                        );
                    }
                }
            }
            
            return centroids;
        }
        
        private static Color[] LabColorMatching(Color[] targetPixels, Color referenceColor, 
            float intensity, bool preserveLuminance)
        {
            var referenceLab = ColorSpaceConverter.RGBtoLAB(referenceColor);
            var adjustedPixels = new Color[targetPixels.Length];
            
            for (int i = 0; i < targetPixels.Length; i++)
            {
                var targetLab = ColorSpaceConverter.RGBtoLAB(targetPixels[i]);
                
                // Shift towards reference color in LAB space
                var adjustedLab = new Vector3(
                    targetLab.x,
                    Mathf.Lerp(targetLab.y, referenceLab.y, intensity),
                    Mathf.Lerp(targetLab.z, referenceLab.z, intensity)
                );
                
                Color adjustedColor = ColorSpaceConverter.LABtoRGB(adjustedLab, targetPixels[i].a);

                if (preserveLuminance)
                {
                    adjustedColor = ColorSpaceConverter.PreserveLuminance(targetPixels[i], adjustedColor);
                }

                adjustedPixels[i] = adjustedColor;
            }

            return adjustedPixels;
        }

        private static Color[] HueShiftToColor(Color[] targetPixels, Color referenceColor, 
            float intensity, bool preserveLuminance)
        {
            var referenceHsv = ColorSpaceConverter.RGBtoHSV(referenceColor);
            var adjustedPixels = new Color[targetPixels.Length];
            
            for (int i = 0; i < targetPixels.Length; i++)
            {
                var targetHsv = ColorSpaceConverter.RGBtoHSV(targetPixels[i]);
                
                // Shift hue towards reference
                float targetHue = targetHsv.x;
                float referenceHue = referenceHsv.x;
                
                // Calculate shortest path between hues
                float hueDiff = referenceHue - targetHue;
                if (hueDiff > 180) hueDiff -= 360;
                if (hueDiff < -180) hueDiff += 360;
                
                var adjustedHsv = new Vector3(
                    (targetHue + hueDiff * intensity) % 360,
                    targetHsv.y,
                    targetHsv.z
                );
                
                if (adjustedHsv.x < 0) adjustedHsv.x += 360;
                
                Color adjustedColor = ColorSpaceConverter.HSVtoRGB(adjustedHsv, targetPixels[i].a);

                if (preserveLuminance)
                {
                    adjustedColor = ColorSpaceConverter.PreserveLuminance(targetPixels[i], adjustedColor);
                }

                adjustedPixels[i] = adjustedColor;
            }

            return adjustedPixels;
        }

        private static Color[] ColorTransferToColor(Color[] targetPixels, Color referenceColor, 
            float intensity, bool preserveLuminance)
        {
            var adjustedPixels = new Color[targetPixels.Length];
            
            for (int i = 0; i < targetPixels.Length; i++)
            {
                Color original = targetPixels[i];
                
                // Blend towards reference color
                Color blendedColor = Color.Lerp(original, referenceColor, intensity * 0.5f);
                
                if (preserveLuminance)
                {
                    blendedColor = ColorSpaceConverter.PreserveLuminance(original, blendedColor);
                }
                
                adjustedPixels[i] = blendedColor;
            }
            
            return adjustedPixels;
        }
        
        private static Color[] AdaptiveAdjustmentToColor(Color[] targetPixels, Color referenceColor, 
            float intensity, bool preserveLuminance)
        {
            // Combine LAB and hue shift approaches
            var labResult = LabColorMatching(targetPixels, referenceColor, intensity * 0.7f, preserveLuminance);
            var hueResult = HueShiftToColor(targetPixels, referenceColor, intensity * 0.3f, preserveLuminance);
            
            var adjustedPixels = new Color[targetPixels.Length];
            for (int i = 0; i < targetPixels.Length; i++)
            {
                adjustedPixels[i] = ColorSpaceConverter.BlendColors(labResult[i], hueResult[i], 0.6f);
            }
            
            return adjustedPixels;
        }
        
        private static Color[] CreateSyntheticReferenceWithMainColor(Color[] originalReference, Color targetMainColor)
        {
            // Extract dominant colors from original reference
            var dominantColors = ExtractDominantColors(originalReference, 5);
            
            // Replace the most dominant color with the target main color
            if (dominantColors.Count > 0)
            {
                dominantColors[0] = targetMainColor;
            }
            else
            {
                dominantColors.Add(targetMainColor);
            }
            
            // Create synthetic reference array maintaining original structure but with new main color
            var syntheticReference = new Color[originalReference.Length];
            var random = new System.Random(42); // Use fixed seed for consistency
            
            for (int i = 0; i < originalReference.Length; i++)
            {
                // 60% chance of using the target main color, 40% chance of using other colors
                if (random.NextDouble() < 0.6)
                {
                    syntheticReference[i] = targetMainColor;
                }
                else if (dominantColors.Count > 1)
                {
                    int colorIndex = random.Next(1, dominantColors.Count);
                    syntheticReference[i] = dominantColors[colorIndex];
                }
                else
                {
                    syntheticReference[i] = targetMainColor;
                }
            }
            
            return syntheticReference;
        }
        
        private static List<Color> ExtractDominantColors(Color[] pixels, int colorCount)
        {
            if (pixels.Length == 0 || colorCount <= 0)
                return new List<Color>();
            
            // Simplified k-means clustering for performance
            var random = new System.Random();
            var centroids = new List<Color>();
            
            // Initialize centroids randomly
            for (int i = 0; i < colorCount; i++)
            {
                centroids.Add(pixels[random.Next(pixels.Length)]);
            }
            
            // Simplified iteration (fewer iterations for performance)
            for (int iteration = 0; iteration < 5; iteration++)
            {
                var clusters = new List<Color>[colorCount];
                for (int i = 0; i < colorCount; i++)
                {
                    clusters[i] = new List<Color>();
                }
                
                // Assign pixels to clusters (sample every 10th pixel for performance)
                for (int i = 0; i < pixels.Length; i += 10)
                {
                    var pixel = pixels[i];
                    int nearestCentroid = 0;
                    float minDistance = ColorSpaceConverter.ColorDistanceRGB(pixel, centroids[0]);
                    
                    for (int j = 1; j < colorCount; j++)
                    {
                        float distance = ColorSpaceConverter.ColorDistanceRGB(pixel, centroids[j]);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            nearestCentroid = j;
                        }
                    }
                    
                    clusters[nearestCentroid].Add(pixel);
                }
                
                // Update centroids
                for (int i = 0; i < colorCount; i++)
                {
                    if (clusters[i].Count > 0)
                    {
                        float r = 0, g = 0, b = 0;
                        foreach (var color in clusters[i])
                        {
                            r += color.r;
                            g += color.g;
                            b += color.b;
                        }
                        
                        centroids[i] = new Color(
                            r / clusters[i].Count,
                            g / clusters[i].Count,
                            b / clusters[i].Count,
                            1.0f
                        );
                    }
                }
            }
            
            return centroids;
        }
        
        // High-precision color adjustment using synthetic reference from UV-used areas
        public static Color[] AdjustColors(Color[] targetPixels, Color[] syntheticReferencePixels, 
            float intensity, bool preserveLuminance, ColorAdjustmentMode mode)
        {
            if (targetPixels == null || syntheticReferencePixels == null)
                return targetPixels;
            
            switch (mode)
            {
                case ColorAdjustmentMode.LabHistogramMatching:
                    return LabHistogramMatching(targetPixels, syntheticReferencePixels, intensity, preserveLuminance);
                    
                case ColorAdjustmentMode.HueShift:
                    return HueShiftAdjustment(targetPixels, syntheticReferencePixels, intensity, preserveLuminance);
                    
                case ColorAdjustmentMode.ColorTransfer:
                    return ColorTransferAdjustment(targetPixels, syntheticReferencePixels, intensity, preserveLuminance);
                    
                case ColorAdjustmentMode.AdaptiveAdjustment:
                    return AdaptiveAdjustment(targetPixels, syntheticReferencePixels, intensity, preserveLuminance);
                    
                default:
                    return targetPixels;
            }
        }
        
        private static TextureStructureInfo AnalyzeTextureStructure(Color[] pixels)
        {
            var info = new TextureStructureInfo();
            
            if (pixels.Length == 0)
                return info;
            
            float minBrightness = 1f;
            float maxBrightness = 0f;
            float totalHue = 0f;
            int validHueCount = 0;
            float totalSaturation = 0f;
            
            foreach (var pixel in pixels)
            {
                // Calculate brightness
                float brightness = 0.299f * pixel.r + 0.587f * pixel.g + 0.114f * pixel.b;
                minBrightness = Mathf.Min(minBrightness, brightness);
                maxBrightness = Mathf.Max(maxBrightness, brightness);
                
                // Calculate HSV for hue analysis
                Vector3 hsv = ColorSpaceConverter.RGBtoHSV(pixel);
                totalSaturation += hsv.y;
                
                if (hsv.y > 0.1f) // Only consider saturated colors for hue
                {
                    totalHue += hsv.x;
                    validHueCount++;
                }
            }
            
            info.minBrightness = minBrightness;
            info.maxBrightness = maxBrightness;
            info.averageSaturation = totalSaturation / pixels.Length;
            info.dominantHue = validHueCount > 0 ? totalHue / validHueCount : 0f;
            info.brightnessDelta = maxBrightness - minBrightness;
            
            return info;
        }
        
        private static Color ApplyTextureAwareColorReplacement(Color originalPixel, Color targetColor, Color referenceColor,
            float similarity, float selectionRange, float intensity, TextureStructureInfo textureInfo)
        {
            // Calculate comprehensive RGB ratio transformation
            // This preserves all color variations as ratios relative to the target color
            Vector3 colorRatio = CalculateColorRatio(originalPixel, targetColor);
            
            // Apply the exact same ratios to the reference color
            Color transformedColor = ApplyColorRatio(referenceColor, colorRatio);
            
            // For extreme transformations, use direct replacement to avoid color tint preservation
            // Only blend with original if the transformation is very weak
            if (intensity >= 0.8f && similarity > (1.0f - selectionRange))
            {
                // Direct replacement for high intensity and high similarity - no color blending
                return transformedColor;
            }
            else if (similarity > (1.0f - selectionRange))
            {
                // High similarity area but lower intensity - minimal blending
                float transformStrength = similarity * intensity;
                return Color.Lerp(originalPixel, transformedColor, transformStrength);
            }
            else
            {
                // Lower similarity area - even more gentle transformation
                float transformStrength = intensity * 0.5f * (similarity * similarity);
                return Color.Lerp(originalPixel, transformedColor, transformStrength);
            }
        }
        
        private static Vector3 CalculateColorRatio(Color originalPixel, Color targetColor)
        {
            // Calculate ratio for each RGB component with safe division
            // This preserves the exact relationship: originalPixel = targetColor * ratio
            const float minColorValue = 0.001f;
            
            Vector3 ratio = new Vector3(
                targetColor.r > minColorValue ? originalPixel.r / targetColor.r : (originalPixel.r > minColorValue ? originalPixel.r / minColorValue : 1f),
                targetColor.g > minColorValue ? originalPixel.g / targetColor.g : (originalPixel.g > minColorValue ? originalPixel.g / minColorValue : 1f),
                targetColor.b > minColorValue ? originalPixel.b / targetColor.b : (originalPixel.b > minColorValue ? originalPixel.b / minColorValue : 1f)
            );
            
            // Clamp ratios to reasonable range to prevent extreme values
            ratio.x = Mathf.Clamp(ratio.x, 0f, 10f);
            ratio.y = Mathf.Clamp(ratio.y, 0f, 10f);
            ratio.z = Mathf.Clamp(ratio.z, 0f, 10f);
            
            return ratio;
        }
        
        private static Color ApplyColorRatio(Color baseColor, Vector3 ratio)
        {
            // Apply the ratio to each RGB component
            return new Color(
                Mathf.Clamp01(baseColor.r * ratio.x),
                Mathf.Clamp01(baseColor.g * ratio.y),
                Mathf.Clamp01(baseColor.b * ratio.z),
                1f
            );
        }
        
    }
    
    [Serializable]
    public class LabStatistics
    {
        public float lMean, aMean, bMean;
        public float lStd, aStd, bStd;
    }
    
    [Serializable]
    public class ColorStatistics
    {
        public float rMean, gMean, bMean;
        public float rStd, gStd, bStd;
    }
    
    [Serializable]
    public class TextureStructureInfo
    {
        public float minBrightness;
        public float maxBrightness;
        public float brightnessDelta;
        public float averageSaturation;
        public float dominantHue;
    }
    
    public static class TransparencyUtils
    {
        // Preserve transparency by restoring original alpha values for transparent pixels
        public static Color[] PreserveTransparency(Color[] originalPixels, Color[] adjustedPixels)
        {
            if (originalPixels == null || adjustedPixels == null || originalPixels.Length != adjustedPixels.Length)
                return adjustedPixels;
            
            for (int i = 0; i < originalPixels.Length; i++)
            {
                Color original = originalPixels[i];
                
                // If original pixel is transparent, preserve it completely
                if (original.a < ColorAdjuster.ALPHA_THRESHOLD)
                {
                    adjustedPixels[i] = original;
                }
                else
                {
                    // Always preserve the original alpha value
                    adjustedPixels[i].a = original.a;
                }
            }
            
            return adjustedPixels;
        }
        
        // Filter out transparent pixels from color analysis
        public static Color[] FilterOpaquePixels(Color[] pixels)
        {
            if (pixels == null) return null;
            
            var opaquePixels = new List<Color>();
            foreach (var pixel in pixels)
            {
                if (pixel.a >= ColorAdjuster.ALPHA_THRESHOLD)
                {
                    opaquePixels.Add(pixel);
                }
            }
            
            return opaquePixels.Count > 0 ? opaquePixels.ToArray() : new Color[] { Color.white };
        }
        
        // Check if a pixel is considered transparent
        public static bool IsTransparent(Color pixel)
        {
            return pixel.a < ColorAdjuster.ALPHA_THRESHOLD;
        }
    }
}