using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TexColAdjuster
{
    // TexColAdjuster optimized pixel structure
    // High-performance alternative to Unity's Color struct for bulk processing
    [StructLayout(LayoutKind.Sequential)]
    public struct ColorPixel : IEquatable<ColorPixel>
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public ColorPixel(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public ColorPixel(Color color)
        {
            R = (byte)(Mathf.Clamp01(color.r) * 255);
            G = (byte)(Mathf.Clamp01(color.g) * 255);
            B = (byte)(Mathf.Clamp01(color.b) * 255);
            A = (byte)(Mathf.Clamp01(color.a) * 255);
        }

        public Color ToColor()
        {
            return new Color(R / 255f, G / 255f, B / 255f, A / 255f);
        }

        // Fast RGB distance calculation for smart color matching
        public float DistanceTo(ColorPixel other)
        {
            float rDiff = (R - other.R) / 255f;
            float gDiff = (G - other.G) / 255f;
            float bDiff = (B - other.B) / 255f;
            return Mathf.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
        }

        // Calculate color difference for difference-based transformation
        public ColorPixel GetDifference(ColorPixel target)
        {
            return new ColorPixel(
                (byte)Mathf.Clamp(target.R - R, -255, 255),
                (byte)Mathf.Clamp(target.G - G, -255, 255),
                (byte)Mathf.Clamp(target.B - B, -255, 255),
                A
            );
        }

        // Apply color difference (TexColAdjuster smart matching algorithm)
        public ColorPixel ApplyDifference(ColorPixel difference, float intensity = 1.0f)
        {
            int newR = R + (int)((sbyte)difference.R * intensity);
            int newG = G + (int)((sbyte)difference.G * intensity);
            int newB = B + (int)((sbyte)difference.B * intensity);

            return new ColorPixel(
                (byte)Mathf.Clamp(newR, 0, 255),
                (byte)Mathf.Clamp(newG, 0, 255),
                (byte)Mathf.Clamp(newB, 0, 255),
                A
            );
        }

        // Calculate brightness for selection algorithms
        public float GetBrightness()
        {
            return (R * 0.299f + G * 0.587f + B * 0.114f) / 255f;
        }

        // Gamma correction
        public ColorPixel ApplyGamma(float gamma)
        {
            if (gamma <= 0) gamma = 1.0f;
            
            float invGamma = 1.0f / gamma;
            return new ColorPixel(
                (byte)(255 * Mathf.Pow(R / 255f, invGamma)),
                (byte)(255 * Mathf.Pow(G / 255f, invGamma)),
                (byte)(255 * Mathf.Pow(B / 255f, invGamma)),
                A
            );
        }

        // Brightness and contrast adjustment
        public ColorPixel ApplyBrightnessContrast(float brightness, float contrast)
        {
            float r = ((R / 255f - 0.5f) * contrast + 0.5f) * brightness;
            float g = ((G / 255f - 0.5f) * contrast + 0.5f) * brightness;
            float b = ((B / 255f - 0.5f) * contrast + 0.5f) * brightness;

            return new ColorPixel(
                (byte)(255 * Mathf.Clamp01(r)),
                (byte)(255 * Mathf.Clamp01(g)),
                (byte)(255 * Mathf.Clamp01(b)),
                A
            );
        }

        // Blend with another pixel
        public ColorPixel Blend(ColorPixel other, float factor)
        {
            factor = Mathf.Clamp01(factor);
            
            return new ColorPixel(
                (byte)Mathf.Lerp(R, other.R, factor),
                (byte)Mathf.Lerp(G, other.G, factor),
                (byte)Mathf.Lerp(B, other.B, factor),
                (byte)Mathf.Lerp(A, other.A, factor)
            );
        }

        public bool Equals(ColorPixel other)
        {
            return R == other.R && G == other.G && B == other.B && A == other.A;
        }

        public override bool Equals(object obj)
        {
            return obj is ColorPixel other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (R << 24) | (G << 16) | (B << 8) | A;
        }

        public static bool operator ==(ColorPixel left, ColorPixel right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ColorPixel left, ColorPixel right)
        {
            return !left.Equals(right);
        }
    }

    // Configuration for TexColAdjuster's intelligent color transformation
    [Serializable]
    public class ColorTransformConfig
    {
        [Range(0f, 2f)]
        public float brightness = 1.0f;
        
        [Range(0f, 2f)]
        public float contrast = 1.0f;
        
        [Range(0.1f, 3f)]
        public float gamma = 1.0f;
        
        [Range(0f, 1f)]
        public float transparency = 0.0f;
        
        [Range(0f, 1f)]
        public float intensity = 1.0f;

        // TexColAdjuster processing modes
        public enum BalanceMode
        {
            Simple,     // V1: Basic difference application
            Weighted,   // V2: Distance-based weighting
            Advanced    // V3: Gradient and area-based processing
        }
        
        public BalanceMode balanceMode = BalanceMode.Weighted;
        
        [Range(0.1f, 2f)]
        public float selectionRadius = 1.0f;
        
        [Range(0f, 1f)]
        public float minSimilarity = 0.1f;
    }
}