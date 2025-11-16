using System;
using UnityEngine;

namespace TexColAdjuster.Editor
{
    public static class ColorSpaceConverter
    {
        public static Vector3 RGBtoLAB(Color rgb)
        {
            // First convert RGB to XYZ
            Vector3 xyz = RGBtoXYZ(rgb);
            
            // Then convert XYZ to LAB
            return XYZtoLAB(xyz);
        }
        
        public static Color LABtoRGB(Vector3 lab)
        {
            // First convert LAB to XYZ
            Vector3 xyz = LABtoXYZ(lab);
            
            // Then convert XYZ to RGB
            return XYZtoRGB(xyz);
        }
        
        public static Vector3 RGBtoXYZ(Color rgb)
        {
            // Normalize RGB values
            float r = rgb.r;
            float g = rgb.g;
            float b = rgb.b;
            
            // Apply gamma correction
            r = (r > 0.04045f) ? Mathf.Pow((r + 0.055f) / 1.055f, 2.4f) : r / 12.92f;
            g = (g > 0.04045f) ? Mathf.Pow((g + 0.055f) / 1.055f, 2.4f) : g / 12.92f;
            b = (b > 0.04045f) ? Mathf.Pow((b + 0.055f) / 1.055f, 2.4f) : b / 12.92f;
            
            // Convert to XYZ using sRGB matrix
            float x = r * 0.4124564f + g * 0.3575761f + b * 0.1804375f;
            float y = r * 0.2126729f + g * 0.7151522f + b * 0.0721750f;
            float z = r * 0.0193339f + g * 0.1191920f + b * 0.9503041f;
            
            return new Vector3(x, y, z);
        }
        
        public static Color XYZtoRGB(Vector3 xyz)
        {
            // Convert XYZ to RGB using sRGB matrix
            float r = xyz.x * 3.2404542f + xyz.y * -1.5371385f + xyz.z * -0.4985314f;
            float g = xyz.x * -0.9692660f + xyz.y * 1.8760108f + xyz.z * 0.0415560f;
            float b = xyz.x * 0.0556434f + xyz.y * -0.2040259f + xyz.z * 1.0572252f;
            
            // Apply inverse gamma correction
            r = (r > 0.0031308f) ? 1.055f * Mathf.Pow(r, 1.0f / 2.4f) - 0.055f : 12.92f * r;
            g = (g > 0.0031308f) ? 1.055f * Mathf.Pow(g, 1.0f / 2.4f) - 0.055f : 12.92f * g;
            b = (b > 0.0031308f) ? 1.055f * Mathf.Pow(b, 1.0f / 2.4f) - 0.055f : 12.92f * b;
            
            // Clamp values
            r = Mathf.Clamp01(r);
            g = Mathf.Clamp01(g);
            b = Mathf.Clamp01(b);
            
            return new Color(r, g, b, 1.0f);
        }
        
        public static Vector3 XYZtoLAB(Vector3 xyz)
        {
            // D65 illuminant reference white
            const float Xn = 0.95047f;
            const float Yn = 1.00000f;
            const float Zn = 1.08883f;
            
            float fx = LabF(xyz.x / Xn);
            float fy = LabF(xyz.y / Yn);
            float fz = LabF(xyz.z / Zn);
            
            float L = 116.0f * fy - 16.0f;
            float a = 500.0f * (fx - fy);
            float b = 200.0f * (fy - fz);
            
            return new Vector3(L, a, b);
        }
        
        public static Vector3 LABtoXYZ(Vector3 lab)
        {
            // D65 illuminant reference white
            const float Xn = 0.95047f;
            const float Yn = 1.00000f;
            const float Zn = 1.08883f;
            
            float fy = (lab.x + 16.0f) / 116.0f;
            float fx = lab.y / 500.0f + fy;
            float fz = fy - lab.z / 200.0f;
            
            float x = Xn * LabFInv(fx);
            float y = Yn * LabFInv(fy);
            float z = Zn * LabFInv(fz);
            
            return new Vector3(x, y, z);
        }
        
        private static float LabF(float t)
        {
            const float delta = 6.0f / 29.0f;
            if (t > delta * delta * delta)
                return Mathf.Pow(t, 1.0f / 3.0f);
            else
                return t / (3.0f * delta * delta) + 4.0f / 29.0f;
        }
        
        private static float LabFInv(float t)
        {
            const float delta = 6.0f / 29.0f;
            if (t > delta)
                return t * t * t;
            else
                return 3.0f * delta * delta * (t - 4.0f / 29.0f);
        }
        
        public static Vector3 RGBtoHSV(Color rgb)
        {
            float r = rgb.r;
            float g = rgb.g;
            float b = rgb.b;
            
            float max = Mathf.Max(r, Mathf.Max(g, b));
            float min = Mathf.Min(r, Mathf.Min(g, b));
            float delta = max - min;
            
            float h = 0.0f;
            float s = 0.0f;
            float v = max;
            
            if (delta != 0.0f)
            {
                s = delta / max;
                
                if (max == r)
                    h = ((g - b) / delta) % 6.0f;
                else if (max == g)
                    h = (b - r) / delta + 2.0f;
                else
                    h = (r - g) / delta + 4.0f;
                
                h *= 60.0f;
                if (h < 0.0f)
                    h += 360.0f;
            }
            
            return new Vector3(h, s, v);
        }
        
        public static Color HSVtoRGB(Vector3 hsv)
        {
            float h = hsv.x;
            float s = hsv.y;
            float v = hsv.z;
            
            float c = v * s;
            float x = c * (1.0f - Mathf.Abs(((h / 60.0f) % 2.0f) - 1.0f));
            float m = v - c;
            
            float r = 0.0f, g = 0.0f, b = 0.0f;
            
            if (h >= 0.0f && h < 60.0f)
            {
                r = c; g = x; b = 0.0f;
            }
            else if (h >= 60.0f && h < 120.0f)
            {
                r = x; g = c; b = 0.0f;
            }
            else if (h >= 120.0f && h < 180.0f)
            {
                r = 0.0f; g = c; b = x;
            }
            else if (h >= 180.0f && h < 240.0f)
            {
                r = 0.0f; g = x; b = c;
            }
            else if (h >= 240.0f && h < 300.0f)
            {
                r = x; g = 0.0f; b = c;
            }
            else if (h >= 300.0f && h < 360.0f)
            {
                r = c; g = 0.0f; b = x;
            }
            
            return new Color(r + m, g + m, b + m, 1.0f);
        }
        
        public static float GetLuminance(Color color)
        {
            return 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
        }
        
        public static Color PreserveLuminance(Color originalColor, Color newColor)
        {
            float originalLuminance = GetLuminance(originalColor);
            float newLuminance = GetLuminance(newColor);
            
            if (newLuminance == 0.0f)
                return originalColor;
            
            float ratio = originalLuminance / newLuminance;
            
            return new Color(
                Mathf.Clamp01(newColor.r * ratio),
                Mathf.Clamp01(newColor.g * ratio),
                Mathf.Clamp01(newColor.b * ratio),
                originalColor.a
            );
        }
        
        public static Color BlendColors(Color color1, Color color2, float blend)
        {
            blend = Mathf.Clamp01(blend);
            
            return new Color(
                Mathf.Lerp(color1.r, color2.r, blend),
                Mathf.Lerp(color1.g, color2.g, blend),
                Mathf.Lerp(color1.b, color2.b, blend),
                Mathf.Lerp(color1.a, color2.a, blend)
            );
        }
        
        public static Vector3 BlendLAB(Vector3 lab1, Vector3 lab2, float blend)
        {
            blend = Mathf.Clamp01(blend);
            
            return new Vector3(
                Mathf.Lerp(lab1.x, lab2.x, blend),
                Mathf.Lerp(lab1.y, lab2.y, blend),
                Mathf.Lerp(lab1.z, lab2.z, blend)
            );
        }
        
        public static float ColorDistance(Color color1, Color color2)
        {
            Vector3 lab1 = RGBtoLAB(color1);
            Vector3 lab2 = RGBtoLAB(color2);
            
            return Vector3.Distance(lab1, lab2);
        }
        
        public static float ColorDistanceRGB(Color color1, Color color2)
        {
            return Mathf.Sqrt(
                Mathf.Pow(color1.r - color2.r, 2) +
                Mathf.Pow(color1.g - color2.g, 2) +
                Mathf.Pow(color1.b - color2.b, 2)
            );
        }
        
        public static Color ApplyGammaSaturationBrightness(Color color, float gamma, float saturation, float brightness)
        {
            // Convert to HSV for saturation adjustment
            Vector3 hsv = RGBtoHSV(color);

            // For backward compatibility this function applied saturation, brightness, then gamma.
            // We'll keep it but also provide a more general function supporting hue shift.
            hsv.y *= saturation;
            hsv.y = Mathf.Clamp01(hsv.y);

            hsv.z *= brightness;
            hsv.z = Mathf.Clamp01(hsv.z);

            Color adjustedColor = HSVtoRGB(hsv);

            // Use direct exponentiation so that gamma < 1 makes the image lighter and gamma > 1 makes it darker.
            if (gamma <= 0f) gamma = 1f;
            adjustedColor.r = Mathf.Clamp01(Mathf.Pow(adjustedColor.r, gamma));
            adjustedColor.g = Mathf.Clamp01(Mathf.Pow(adjustedColor.g, gamma));
            adjustedColor.b = Mathf.Clamp01(Mathf.Pow(adjustedColor.b, gamma));

            return adjustedColor;
        }

        /// <summary>
        /// Apply hue shift (degrees), saturation multiplier, brightness multiplier and gamma correction to a color.
        /// Hue shift is specified in degrees and wraps around 0-360.
        /// </summary>
        public static Color ApplyHSBG(Color color, float hueShiftDegrees, float saturation, float brightness, float gamma)
        {
            Vector3 hsv = RGBtoHSV(color);

            // Apply hue shift
            hsv.x = (hsv.x + hueShiftDegrees) % 360f;
            if (hsv.x < 0f) hsv.x += 360f;

            // Apply saturation and brightness multipliers
            hsv.y = Mathf.Clamp01(hsv.y * saturation);
            hsv.z = Mathf.Clamp01(hsv.z * brightness);

            Color adjusted = HSVtoRGB(hsv);

            // Apply gamma correction: gamma < 1 => lighter, gamma > 1 => darker
            if (gamma <= 0f) gamma = 1f;
            adjusted.r = Mathf.Clamp01(Mathf.Pow(adjusted.r, gamma));
            adjusted.g = Mathf.Clamp01(Mathf.Pow(adjusted.g, gamma));
            adjusted.b = Mathf.Clamp01(Mathf.Pow(adjusted.b, gamma));

            adjusted.a = color.a;
            return adjusted;
        }

        public static Color[] ApplyHSBGToArray(Color[] colors, float hueShiftDegrees, float saturation, float brightness, float gamma)
        {
            Color[] adjustedColors = new Color[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                adjustedColors[i] = ApplyHSBG(colors[i], hueShiftDegrees, saturation, brightness, gamma);
            }
            return adjustedColors;
        }
        
        public static Color[] ApplyGammaSaturationBrightnessToArray(Color[] colors, float gamma, float saturation, float brightness)
        {
            Color[] adjustedColors = new Color[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                adjustedColors[i] = ApplyGammaSaturationBrightness(colors[i], gamma, saturation, brightness);
            }
            return adjustedColors;
        }
    }
}