using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace TexColAdjuster
{
    public static class TextureExporter
    {
        public static bool SaveTexture(Texture2D texture, string originalPath, SaveOptions options)
        {
            if (texture == null || string.IsNullOrEmpty(originalPath))
                return false;
            
            string savePath = GetSavePath(originalPath, options);
            
            try
            {
                byte[] bytes = GetTextureBytes(texture, options.format);
                File.WriteAllBytes(savePath, bytes);
                
                AssetDatabase.Refresh();
                
                // Update import settings if needed
                if (options.updateImportSettings)
                {
                    UpdateTextureImportSettings(savePath, options.importSettings);
                }
                
                Debug.Log($"Texture saved successfully to: {savePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save texture: {e.Message}");
                return false;
            }
        }
        
        public static bool SaveTextureWithDialog(Texture2D texture, string originalPath, SaveOptions options)
        {
            if (texture == null)
                return false;
            
            string defaultPath = GetSavePath(originalPath, options);
            string directory = Path.GetDirectoryName(defaultPath);
            string filename = Path.GetFileName(defaultPath);
            
            string savePath = EditorUtility.SaveFilePanel(
                "Save Adjusted Texture",
                directory,
                filename,
                GetExtensionFromFormat(options.format)
            );
            
            if (string.IsNullOrEmpty(savePath))
                return false;
            
            try
            {
                byte[] bytes = GetTextureBytes(texture, options.format);
                File.WriteAllBytes(savePath, bytes);
                
                AssetDatabase.Refresh();
                
                if (options.updateImportSettings)
                {
                    UpdateTextureImportSettings(savePath, options.importSettings);
                }
                
                Debug.Log($"Texture saved successfully to: {savePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save texture: {e.Message}");
                return false;
            }
        }
        
        private static string GetSavePath(string originalPath, SaveOptions options)
        {
            string directory = Path.GetDirectoryName(originalPath);
            string filename = Path.GetFileNameWithoutExtension(originalPath);
            string extension = GetExtensionFromFormat(options.format);
            
            if (options.overwriteOriginal)
            {
                return Path.Combine(directory, filename + extension);
            }
            else
            {
                string suffix = options.suffix;
                if (string.IsNullOrEmpty(suffix))
                    suffix = "_adjusted";
                
                return Path.Combine(directory, filename + suffix + extension);
            }
        }
        
        private static byte[] GetTextureBytes(Texture2D texture, TextureExportFormat format)
        {
            switch (format)
            {
                case TextureExportFormat.PNG:
                    return texture.EncodeToPNG();
                case TextureExportFormat.JPG:
                    return texture.EncodeToJPG();
                case TextureExportFormat.TGA:
                    return texture.EncodeToTGA();
                default:
                    return texture.EncodeToPNG();
            }
        }
        
        private static string GetExtensionFromFormat(TextureExportFormat format)
        {
            switch (format)
            {
                case TextureExportFormat.PNG:
                    return ".png";
                case TextureExportFormat.JPG:
                    return ".jpg";
                case TextureExportFormat.TGA:
                    return ".tga";
                default:
                    return ".png";
            }
        }
        
        private static void UpdateTextureImportSettings(string path, TextureImportSettings settings)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;
            
            importer.textureType = settings.textureType;
            importer.isReadable = settings.isReadable;
            importer.mipmapEnabled = settings.generateMipmaps;
            importer.alphaSource = settings.alphaSource;
            importer.alphaIsTransparency = settings.alphaIsTransparency;
            importer.maxTextureSize = settings.maxTextureSize;
            importer.textureCompression = settings.compression;
            
            AssetDatabase.ImportAsset(path);
        }
        
        public static void BatchSaveTextures(BatchSaveData batchData)
        {
            if (batchData.textures == null || batchData.textures.Count == 0)
                return;
            
            int totalTextures = batchData.textures.Count;
            
            for (int i = 0; i < totalTextures; i++)
            {
                var textureData = batchData.textures[i];
                
                if (EditorUtility.DisplayCancelableProgressBar(
                    "Batch Saving Textures",
                    $"Saving {textureData.name} ({i + 1}/{totalTextures})",
                    (float)i / totalTextures))
                {
                    break;
                }
                
                SaveTexture(textureData.texture, textureData.originalPath, batchData.saveOptions);
            }
            
            EditorUtility.ClearProgressBar();
        }
        
        public static string GenerateProcessingReport(ProcessingReport report)
        {
            var reportText = new System.Text.StringBuilder();
            
            reportText.AppendLine("=== TexColAdjuster Processing Report ===");
            reportText.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            reportText.AppendLine();
            
            reportText.AppendLine("Settings:");
            reportText.AppendLine($"  Adjustment Intensity: {report.adjustmentIntensity:F1}%");
            reportText.AppendLine($"  Preserve Luminance: {report.preserveLuminance}");
            reportText.AppendLine($"  Adjustment Mode: {report.adjustmentMode}");
            reportText.AppendLine();
            
            reportText.AppendLine("Processed Textures:");
            foreach (var texture in report.processedTextures)
            {
                reportText.AppendLine($"  - {texture.name} ({texture.width}x{texture.height})");
                reportText.AppendLine($"    Original: {texture.originalPath}");
                reportText.AppendLine($"    Output: {texture.outputPath}");
                reportText.AppendLine($"    Status: {texture.status}");
                reportText.AppendLine();
            }
            
            reportText.AppendLine($"Total Processed: {report.processedTextures.Count}");
            reportText.AppendLine($"Successful: {report.processedTextures.Count(t => t.status == ProcessingStatus.Success)}");
            reportText.AppendLine($"Failed: {report.processedTextures.Count(t => t.status == ProcessingStatus.Failed)}");
            
            return reportText.ToString();
        }
        
        public static void SaveProcessingReport(ProcessingReport report, string path)
        {
            try
            {
                string reportText = GenerateProcessingReport(report);
                File.WriteAllText(path, reportText);
                Debug.Log($"Processing report saved to: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save processing report: {e.Message}");
            }
        }
    }
    
    [Serializable]
    public class SaveOptions
    {
        public bool overwriteOriginal = false;
        public string suffix = "_adjusted";
        public TextureExportFormat format = TextureExportFormat.PNG;
        public bool updateImportSettings = true;
        public TextureImportSettings importSettings = new TextureImportSettings();
    }
    
    [Serializable]
    public class TextureImportSettings
    {
        public TextureImporterType textureType = TextureImporterType.Default;
        public bool isReadable = true;
        public bool generateMipmaps = true;
        public TextureImporterAlphaSource alphaSource = TextureImporterAlphaSource.FromInput;
        public bool alphaIsTransparency = false;
        public int maxTextureSize = 2048;
        public TextureImporterCompression compression = TextureImporterCompression.Uncompressed;
    }
    
    [Serializable]
    public class BatchSaveData
    {
        public List<TextureData> textures = new List<TextureData>();
        public SaveOptions saveOptions = new SaveOptions();
    }
    
    [Serializable]
    public class TextureData
    {
        public string name;
        public Texture2D texture;
        public string originalPath;
        public int width;
        public int height;
    }
    
    [Serializable]
    public class ProcessingReport
    {
        public float adjustmentIntensity;
        public bool preserveLuminance;
        public ColorAdjustmentMode adjustmentMode;
        public List<ProcessedTextureInfo> processedTextures = new List<ProcessedTextureInfo>();
    }
    
    [Serializable]
    public class ProcessedTextureInfo
    {
        public string name;
        public string originalPath;
        public string outputPath;
        public int width;
        public int height;
        public ProcessingStatus status;
        public string errorMessage;
    }
    
    public enum TextureExportFormat
    {
        PNG,
        JPG,
        TGA
    }
    
    public enum ProcessingStatus
    {
        Success,
        Failed,
        Skipped
    }
    
    public static class SaveOptionsExtensions
    {
        public static SaveOptions GetDefaultVRChatSettings()
        {
            return new SaveOptions
            {
                overwriteOriginal = false,
                suffix = "_adjusted",
                format = TextureExportFormat.PNG,
                updateImportSettings = true,
                importSettings = new TextureImportSettings
                {
                    textureType = TextureImporterType.Default,
                    isReadable = false,
                    generateMipmaps = true,
                    alphaSource = TextureImporterAlphaSource.FromInput,
                    alphaIsTransparency = true,
                    maxTextureSize = 2048,
                    compression = TextureImporterCompression.Compressed
                }
            };
        }
        
        public static SaveOptions GetHighQualitySettings()
        {
            return new SaveOptions
            {
                overwriteOriginal = false,
                suffix = "_hq_adjusted",
                format = TextureExportFormat.PNG,
                updateImportSettings = true,
                importSettings = new TextureImportSettings
                {
                    textureType = TextureImporterType.Default,
                    isReadable = false,
                    generateMipmaps = true,
                    alphaSource = TextureImporterAlphaSource.FromInput,
                    alphaIsTransparency = true,
                    maxTextureSize = 4096,
                    compression = TextureImporterCompression.Uncompressed
                }
            };
        }
    }
}