using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TexColAdjuster
{
    // Class to store original texture import settings for restoration
    [Serializable]
    public class TextureImportBackup
    {
        public string texturePath;
        public bool isReadable;
        public TextureImporterCompression compression;
        public TextureFormat textureFormat;
        
        public TextureImportBackup(string path, TextureImporter importer, Texture2D texture)
        {
            texturePath = path;
            isReadable = importer.isReadable;
            compression = importer.textureCompression;
            textureFormat = texture.format;
        }
        
        public void RestoreSettings()
        {
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                bool needsReimport = false;
                
                if (importer.isReadable != isReadable)
                {
                    importer.isReadable = isReadable;
                    needsReimport = true;
                }
                
                if (importer.textureCompression != compression)
                {
                    importer.textureCompression = compression;
                    needsReimport = true;
                }
                
                if (needsReimport)
                {
                    Debug.Log($"Restoring original import settings for: {System.IO.Path.GetFileName(texturePath)}");
                    AssetDatabase.ImportAsset(texturePath);
                }
            }
        }
    }

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
            return MakeTextureReadable(texture, out _);
        }
        
        public static Texture2D MakeTextureReadable(Texture2D texture, out TextureImportBackup backup)
        {
            backup = null;
            
            if (texture == null)
                return null;
                
            // First, try to read pixels directly to check if it's already readable
            try
            {
                texture.GetPixels();
                return texture; // Already readable
            }
            catch
            {
                // Need to make it readable
            }
                
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"Cannot make texture readable: '{texture.name}' - No asset path found. This might be a runtime-created texture.");
                return null;
            }
            
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            
            if (importer != null)
            {
                // Create backup before making changes
                backup = new TextureImportBackup(path, importer, texture);
                
                bool needsReimport = false;
                
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    needsReimport = true;
                }
                
                // For heavily compressed formats (like DXT), temporarily use uncompressed for reading
                if (texture.format == TextureFormat.DXT1 || 
                    texture.format == TextureFormat.DXT1Crunched ||
                    texture.format == TextureFormat.DXT5 || 
                    texture.format == TextureFormat.DXT5Crunched ||
                    !texture.isReadable)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    needsReimport = true;
                }
                
                if (needsReimport)
                {
                    Debug.Log($"Making texture readable: {texture.name} (original format: {texture.format})");
                    AssetDatabase.ImportAsset(path);
                    
                    // Refresh the texture reference after reimport
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    
                    // Verify the texture is now readable
                    try
                    {
                        texture.GetPixels();
                        Debug.Log($"Successfully made texture readable: {texture.name}");
                    }
                    catch (UnityException e)
                    {
                        Debug.LogError($"Failed to make texture readable after reimport: {texture.name}, Error: {e.Message}");
                        // Restore original settings
                        backup.RestoreSettings();
                        return null;
                    }
                }
            }
            else
            {
                Debug.LogWarning($"Could not find TextureImporter for texture: {texture.name} at path: {path}");
                return null;
            }
            
            return texture;
        }
        
        public static Texture2D DuplicateTexture(Texture2D source)
        {
            if (source == null)
                return null;

            var duplicate = TextureColorSpaceUtility.CreateRuntimeTextureLike(source);
            var pixels = TextureUtils.GetPixelsSafe(source);
            if (pixels != null && TextureUtils.SetPixelsSafe(duplicate, pixels))
            {
                return duplicate;
            }

            TextureColorSpaceUtility.UnregisterRuntimeTexture(duplicate);
            UnityEngine.Object.DestroyImmediate(duplicate);
            return null;
        }

        /// <summary>
        /// Creates a readable copy of a texture using RenderTexture (GPU-based, non-destructive)
        /// This method does NOT modify the original texture's import settings
        /// </summary>
        public static Texture2D MakeReadableCopy(Texture2D source)
        {
            if (source == null)
                return null;

            // Create a temporary RenderTexture aligned with the source texture's color space.
            var readWrite = TextureColorSpaceUtility.IsTextureSRGB(source)
                ? RenderTextureReadWrite.sRGB
                : RenderTextureReadWrite.Linear;

            RenderTexture tmp = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                readWrite
            );

            // Blit the source texture to the RenderTexture
            Graphics.Blit(source, tmp);

            // Save the current RenderTexture
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;

            // Create a new readable Texture2D and read pixels from RenderTexture
            Texture2D readableTexture = TextureColorSpaceUtility.CreateRuntimeTextureLike(source);
            readableTexture.ReadPixels(new UnityEngine.Rect(0, 0, tmp.width, tmp.height), 0, 0);
            readableTexture.Apply();

            // Restore the previous RenderTexture
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);

            return readableTexture;
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
            {
                Debug.LogError("Texture validation failed: Texture is null");
                return false;
            }
                
            // Check if texture is readable
            try
            {
                var pixels = texture.GetPixels();
                if (pixels == null || pixels.Length == 0)
                {
                    Debug.LogError($"Texture validation failed: '{texture.name}' returned null or empty pixel array");
                    return false;
                }
                Debug.Log($"Texture validation successful: '{texture.name}' (format: {texture.format}, size: {texture.width}x{texture.height}, pixels: {pixels.Length})");
                return true;
            }
            catch (UnityException e)
            {
                Debug.LogError($"Texture validation failed: '{texture.name}' (format: {texture.format}) - {e.Message}");
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
                
            var resized = TextureColorSpaceUtility.CreateRuntimeTexture(newWidth, newHeight, TextureFormat.RGBA32, false, TextureColorSpaceUtility.IsTextureSRGB(source));
            var pixels = TextureUtils.GetPixelsSafe(source);
            if (pixels == null)
            {
                TextureColorSpaceUtility.UnregisterRuntimeTexture(resized);
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
            
            TextureColorSpaceUtility.UnregisterRuntimeTexture(resized);
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
            {
                Debug.LogError("GetPixelsSafe failed: Texture is null");
                return null;
            }
                
            try
            {
                var pixels = texture.GetPixels();
                TextureColorSpaceUtility.ConvertPixelsToSRGB(texture, pixels);
                if (pixels == null || pixels.Length == 0)
                {
                    Debug.LogError($"GetPixelsSafe failed: '{texture.name}' returned null or empty pixel array (format: {texture.format}, readable: {texture.isReadable})");
                    return null;
                }
                return pixels;
            }
            catch (UnityException e)
            {
                Debug.LogError($"GetPixelsSafe failed: '{texture.name}' (format: {texture.format}, readable: {texture.isReadable}) - {e.Message}");
                Debug.LogError($"Please ensure the texture '{texture.name}' is set to 'Read/Write Enabled' in its import settings, or try reimporting the texture.");
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
                var workingPixels = TextureColorSpaceUtility.ConvertPixelsToTextureSpace(texture, pixels);
                texture.SetPixels(workingPixels);
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
                
            var copy = TextureColorSpaceUtility.CreateRuntimeTextureLike(source);
            var pixels = GetPixelsSafe(source);
            
            if (pixels != null && SetPixelsSafe(copy, pixels))
            {
                return copy;
            }
            
            TextureColorSpaceUtility.UnregisterRuntimeTexture(copy);
            UnityEngine.Object.DestroyImmediate(copy);
            return null;
        }
    }
}
