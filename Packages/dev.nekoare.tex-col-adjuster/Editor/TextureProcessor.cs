using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TexColAdjuster
{
    public static class TextureProcessor
    {
        public static Texture2D LoadTexture(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
                
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
                return null;
                
            return MakeTextureReadable(texture);
        }
        
        public static Texture2D MakeTextureReadable(Texture2D texture)
        {
            if (texture == null)
                return null;
                
            string path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            
            if (importer != null)
            {
                bool needsReimport = false;
                
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    needsReimport = true;
                }
                
                // Only reimport if necessary to avoid changing compression settings
                if (needsReimport)
                {
                    AssetDatabase.ImportAsset(path);
                }
            }
            
            return texture;
        }
        
        public static Texture2D DuplicateTexture(Texture2D source)
        {
            if (source == null)
                return null;
                
            // Use RGBA32 format to ensure SetPixels compatibility
            var duplicate = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            var pixels = TextureUtils.GetPixelsSafe(source);
            if (pixels != null && TextureUtils.SetPixelsSafe(duplicate, pixels))
            {
                return duplicate;
            }
            
            UnityEngine.Object.DestroyImmediate(duplicate);
            return null;
        }
        
        public static void SaveTexture(Texture2D texture, string path, bool overwrite = false)
        {
            if (texture == null || string.IsNullOrEmpty(path))
                return;
                
            byte[] bytes = texture.EncodeToPNG();
            
            if (!overwrite && System.IO.File.Exists(path))
            {
                string directory = System.IO.Path.GetDirectoryName(path);
                string filename = System.IO.Path.GetFileNameWithoutExtension(path);
                string extension = System.IO.Path.GetExtension(path);
                
                int counter = 1;
                string newPath;
                do
                {
                    newPath = System.IO.Path.Combine(directory, $"{filename}_{counter}{extension}");
                    counter++;
                } while (System.IO.File.Exists(newPath));
                
                path = newPath;
            }
            
            System.IO.File.WriteAllBytes(path, bytes);
            AssetDatabase.Refresh();
        }
        
        public static List<Texture2D> LoadMultipleTextures(string[] paths)
        {
            var textures = new List<Texture2D>();
            
            foreach (string path in paths)
            {
                var texture = LoadTexture(path);
                if (texture != null)
                {
                    textures.Add(texture);
                }
            }
            
            return textures;
        }
        
        public static bool ValidateTexture(Texture2D texture)
        {
            if (texture == null)
                return false;
                
            // Check if texture is readable
            try
            {
                var pixels = texture.GetPixels();
                return pixels != null && pixels.Length > 0;
            }
            catch
            {
                return false;
            }
        }
        
        public static Vector2Int GetOptimalSize(Texture2D texture, int maxSize = 4096)
        {
            if (texture == null)
                return Vector2Int.zero;
                
            int width = texture.width;
            int height = texture.height;
            
            // Ensure power of 2 dimensions
            width = Mathf.NextPowerOfTwo(width);
            height = Mathf.NextPowerOfTwo(height);
            
            // Clamp to max size
            if (width > maxSize)
                width = maxSize;
            if (height > maxSize)
                height = maxSize;
                
            return new Vector2Int(width, height);
        }
        
        public static Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
        {
            if (source == null)
                return null;
                
            // Use RGBA32 format to ensure SetPixels compatibility
            var resized = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            var pixels = TextureUtils.GetPixelsSafe(source);
            if (pixels == null)
            {
                UnityEngine.Object.DestroyImmediate(resized);
                return null;
            }
            
            var newPixels = new Color[newWidth * newHeight];
            
            float xRatio = (float)source.width / newWidth;
            float yRatio = (float)source.height / newHeight;
            
            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    int srcX = Mathf.FloorToInt(x * xRatio);
                    int srcY = Mathf.FloorToInt(y * yRatio);
                    
                    srcX = Mathf.Clamp(srcX, 0, source.width - 1);
                    srcY = Mathf.Clamp(srcY, 0, source.height - 1);
                    
                    newPixels[y * newWidth + x] = pixels[srcY * source.width + srcX];
                }
            }
            
            if (TextureUtils.SetPixelsSafe(resized, newPixels))
            {
                return resized;
            }
            
            UnityEngine.Object.DestroyImmediate(resized);
            return null;
        }
        
        public static string[] GetSupportedTextureExtensions()
        {
            return new string[] { ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".psd", ".tiff", ".gif" };
        }
        
        public static bool IsSupportedFormat(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
                
            string extension = System.IO.Path.GetExtension(path).ToLower();
            return Array.Exists(GetSupportedTextureExtensions(), ext => ext == extension);
        }
        
        public static TextureInfo GetTextureInfo(Texture2D texture)
        {
            if (texture == null)
                return null;
                
            return new TextureInfo
            {
                width = texture.width,
                height = texture.height,
                format = texture.format,
                mipmapCount = texture.mipmapCount,
                isReadable = texture.isReadable,
                path = AssetDatabase.GetAssetPath(texture)
            };
        }
    }
    
    [Serializable]
    public class TextureInfo
    {
        public int width;
        public int height;
        public TextureFormat format;
        public int mipmapCount;
        public bool isReadable;
        public string path;
        
        public override string ToString()
        {
            return $"{width}x{height} {format} (Readable: {isReadable})";
        }
    }
    
    public static class TextureUtils
    {
        public static Color[] GetPixelsSafe(Texture2D texture)
        {
            if (texture == null)
                return null;
                
            try
            {
                return texture.GetPixels();
            }
            catch (UnityException e)
            {
                Debug.LogError($"Failed to get pixels from texture: {e.Message}");
                return null;
            }
        }
        
        public static bool SetPixelsSafe(Texture2D texture, Color[] pixels)
        {
            if (texture == null || pixels == null)
                return false;
                
            try
            {
                Debug.Log($"Attempting to set pixels on texture with format: {texture.format}, size: {texture.width}x{texture.height}");
                texture.SetPixels(pixels);
                texture.Apply();
                return true;
            }
            catch (UnityException e)
            {
                Debug.LogError($"Failed to set pixels to texture (format: {texture.format}): {e.Message}");
                return false;
            }
        }
        
        public static Texture2D CreateCopy(Texture2D source)
        {
            if (source == null)
                return null;
                
            // Use RGBA32 format to ensure SetPixels compatibility
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            var pixels = GetPixelsSafe(source);
            
            if (pixels != null && SetPixelsSafe(copy, pixels))
            {
                return copy;
            }
            
            UnityEngine.Object.DestroyImmediate(copy);
            return null;
        }
    }
}
