using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace TexColAdjuster
{
    internal static class TextureColorSpaceUtility
    {
        private class RuntimeTextureInfo
        {
            public bool IsSrgb;
        }

        private static readonly ConditionalWeakTable<Texture, RuntimeTextureInfo> s_RuntimeTextureColorSpace =
            new ConditionalWeakTable<Texture, RuntimeTextureInfo>();

        public static void RegisterRuntimeTexture(Texture texture, bool isSrgb)
        {
            if (texture == null)
                return;

            s_RuntimeTextureColorSpace.Remove(texture);
            s_RuntimeTextureColorSpace.Add(texture, new RuntimeTextureInfo { IsSrgb = isSrgb });
        }

        public static void UnregisterRuntimeTexture(Texture texture)
        {
            if (texture == null)
                return;

            s_RuntimeTextureColorSpace.Remove(texture);
        }

        private static bool? TryGetRuntimeTexture(Texture texture)
        {
            if (texture == null)
                return null;

            if (s_RuntimeTextureColorSpace.TryGetValue(texture, out var info))
                return info.IsSrgb;

            return null;
        }

        private static TextureImporter TryGetImporter(Texture texture)
        {
            if (texture == null)
                return null;

            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
                return null;

            return AssetImporter.GetAtPath(path) as TextureImporter;
        }

        public static bool IsTextureSRGB(Texture texture)
        {
            if (texture == null)
                return false;

            if (TryGetRuntimeTexture(texture) is bool runtimeFlag)
                return runtimeFlag;

            if (texture is RenderTexture renderTexture)
                return renderTexture.sRGB;

            var importer = TryGetImporter(texture);
            if (importer != null)
                return importer.sRGBTexture;

            // Default to sRGB for unknown cases so we keep the historical behaviour.
            return true;
        }

        public static bool RequiresSRGBConversion(Texture texture)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear && IsTextureSRGB(texture);
        }

        public static void ConvertPixelsToSRGB(Texture texture, Color[] pixels)
        {
            if (pixels == null || !RequiresSRGBConversion(texture))
                return;

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = pixels[i].gamma;
            }
        }

        public static Color[] ConvertPixelsToTextureSpace(Texture texture, Color[] pixels, bool cloneIfNeeded = true)
        {
            if (pixels == null || !RequiresSRGBConversion(texture))
                return pixels;

            Color[] workingPixels = cloneIfNeeded ? (Color[])pixels.Clone() : pixels;
            for (int i = 0; i < workingPixels.Length; i++)
            {
                workingPixels[i] = workingPixels[i].linear;
            }

            return workingPixels;
        }

        public static Texture2D CreateRuntimeTexture(int width, int height, TextureFormat format, bool mipChain, bool isSrgb)
        {
            var texture = new Texture2D(width, height, format, mipChain, !isSrgb);
            RegisterRuntimeTexture(texture, isSrgb);
            return texture;
        }

        public static Texture2D CreateRuntimeTextureLike(Texture source, TextureFormat format = TextureFormat.RGBA32, bool mipChain = false)
        {
            bool isSrgb = IsTextureSRGB(source);
            int width = Mathf.Max(1, source != null ? source.width : 1);
            int height = Mathf.Max(1, source != null ? source.height : 1);
            return CreateRuntimeTexture(width, height, format, mipChain, isSrgb);
        }
    }
}
