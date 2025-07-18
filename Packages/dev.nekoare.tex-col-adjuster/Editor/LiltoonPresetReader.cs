using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace TexColAdjuster
{
    public static class LiltoonPresetReader
    {
        private static readonly string[] LiltoonShaderNames = {
            "lilToon",
            "Hidden/lilToonOutline",
            "Hidden/lilToonCutout",
            "Hidden/lilToonTransparent",
            "Hidden/lilToonOnePass",
            "Hidden/lilToonTwoPass"
        };
        
        public static bool IsLiltoonShader(Shader shader)
        {
            if (shader == null) return false;
            
            foreach (string shaderName in LiltoonShaderNames)
            {
                if (shader.name.Contains(shaderName) || shader.name.StartsWith("lilToon"))
                {
                    return true;
                }
            }
            return false;
        }
        
        public static bool IsLiltoonMaterial(Material material)
        {
            return material != null && IsLiltoonShader(material.shader);
        }
        
        public static LiltoonPreset ReadPresetFromAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError("Asset path is null or empty");
                return null;
            }
            
            try
            {
                // Try to load as ScriptableObject first
                var presetAsset = AssetDatabase.LoadAssetAtPath<LiltoonPreset>(assetPath);
                if (presetAsset != null)
                {
                    return presetAsset;
                }
                
                // Try to load as Material and convert
                var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (material != null && IsLiltoonMaterial(material))
                {
                    return GeneratePresetFromMaterial(material);
                }
                
                Debug.LogWarning($"Could not read liltoon preset from: {assetPath}");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error reading preset from {assetPath}: {e.Message}");
                return null;
            }
        }
        
        public static LiltoonPreset GeneratePresetFromMaterial(Material material)
        {
            if (!IsLiltoonMaterial(material))
            {
                Debug.LogError("Material is not using liltoon shader");
                return null;
            }
            
            var preset = ScriptableObject.CreateInstance<LiltoonPreset>();
            preset.presetName = material.name + "_Preset";
            preset.sourceShaderName = material.shader.name;
            preset.description = $"Generated from material: {material.name}";
            
            ExtractMaterialProperties(material, preset);
            
            return preset;
        }
        
        public static LiltoonPreset GenerateSelectivePreset(Material material, string[] targetProperties, string presetName = "", string description = "")
        {
            if (!IsLiltoonMaterial(material))
            {
                Debug.LogError("Material is not using liltoon shader");
                return null;
            }
            
            var preset = ScriptableObject.CreateInstance<LiltoonPreset>();
            preset.presetName = string.IsNullOrEmpty(presetName) ? material.name + "_Selective" : presetName;
            preset.sourceShaderName = material.shader.name;
            preset.description = string.IsNullOrEmpty(description) ? 
                $"Selective preset from: {material.name}" : description;
            
            ExtractSelectiveProperties(material, preset, targetProperties);
            
            return preset;
        }
        
        public static LiltoonPreset GenerateDrawingEffectsPreset(Material material)
        {
            if (!IsLiltoonMaterial(material))
            {
                Debug.LogError("Material is not using liltoon shader");
                return null;
            }
            
            var preset = ScriptableObject.CreateInstance<LiltoonPreset>();
            preset.presetName = material.name + "_DrawingEffects";
            preset.sourceShaderName = material.shader.name;
            preset.description = $"Drawing effects preset from: {material.name} (Lighting, Shadow, sRimShade, Backlight, Rim)";
            
            ExtractSelectiveProperties(material, preset, LiltoonPropertyNames.DrawingEffectProperties);
            
            return preset;
        }
        
        private static void ExtractMaterialProperties(Material material, LiltoonPreset preset)
        {
            var shader = material.shader;
            int propertyCount = ShaderUtil.GetPropertyCount(shader);
            
            for (int i = 0; i < propertyCount; i++)
            {
                string propertyName = ShaderUtil.GetPropertyName(shader, i);
                var propertyType = ShaderUtil.GetPropertyType(shader, i);
                
                try
                {
                    switch (propertyType)
                    {
                        case ShaderUtil.ShaderPropertyType.Color:
                            if (material.HasProperty(propertyName))
                            {
                                Color color = material.GetColor(propertyName);
                                preset.SetColor(propertyName, color);
                            }
                            break;
                            
                        case ShaderUtil.ShaderPropertyType.Float:
                        case ShaderUtil.ShaderPropertyType.Range:
                            if (material.HasProperty(propertyName))
                            {
                                float value = material.GetFloat(propertyName);
                                preset.SetFloat(propertyName, value);
                            }
                            break;
                            
                        case ShaderUtil.ShaderPropertyType.Vector:
                            if (material.HasProperty(propertyName))
                            {
                                Vector4 vector = material.GetVector(propertyName);
                                preset.SetVector(propertyName, vector);
                            }
                            break;
                            
                        case ShaderUtil.ShaderPropertyType.TexEnv:
                            if (material.HasProperty(propertyName))
                            {
                                Texture texture = material.GetTexture(propertyName);
                                if (texture != null)
                                {
                                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture));
                                    preset.SetTextureGuid(propertyName, guid);
                                }
                            }
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to extract property {propertyName}: {e.Message}");
                }
            }
            
            preset.UpdateSerializedData();
        }
        
        private static void ExtractSelectiveProperties(Material material, LiltoonPreset preset, string[] targetProperties)
        {
            var shader = material.shader;
            int propertyCount = ShaderUtil.GetPropertyCount(shader);
            
            // Create a set for faster lookup
            var targetSet = new HashSet<string>(targetProperties);
            
            for (int i = 0; i < propertyCount; i++)
            {
                string propertyName = ShaderUtil.GetPropertyName(shader, i);
                
                // Only process if this property is in our target list
                if (!targetSet.Contains(propertyName))
                    continue;
                
                var propertyType = ShaderUtil.GetPropertyType(shader, i);
                
                try
                {
                    switch (propertyType)
                    {
                        case ShaderUtil.ShaderPropertyType.Color:
                            if (material.HasProperty(propertyName))
                            {
                                Color color = material.GetColor(propertyName);
                                preset.SetColor(propertyName, color);
                            }
                            break;
                            
                        case ShaderUtil.ShaderPropertyType.Float:
                        case ShaderUtil.ShaderPropertyType.Range:
                            if (material.HasProperty(propertyName))
                            {
                                float value = material.GetFloat(propertyName);
                                preset.SetFloat(propertyName, value);
                            }
                            break;
                            
                        case ShaderUtil.ShaderPropertyType.Vector:
                            if (material.HasProperty(propertyName))
                            {
                                Vector4 vector = material.GetVector(propertyName);
                                preset.SetVector(propertyName, vector);
                            }
                            break;
                            
                        case ShaderUtil.ShaderPropertyType.TexEnv:
                            // Skip textures for drawing effects preset as specified
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to extract selective property {propertyName}: {e.Message}");
                }
            }
            
            preset.UpdateSerializedData();
        }
        
        public static void SavePreset(LiltoonPreset preset, string savePath)
        {
            if (preset == null)
            {
                Debug.LogError("Preset is null");
                return;
            }
            
            try
            {
                preset.UpdateSerializedData();
                
                // Ensure directory exists
                string directory = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                AssetDatabase.CreateAsset(preset, savePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                Debug.Log($"Liltoon preset saved to: {savePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save preset: {e.Message}");
            }
        }
        
        public static List<LiltoonPreset> FindAllPresets()
        {
            var presets = new List<LiltoonPreset>();
            
            string[] guids = AssetDatabase.FindAssets("t:LiltoonPreset");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<LiltoonPreset>(assetPath);
                if (preset != null)
                {
                    presets.Add(preset);
                }
            }
            
            return presets;
        }
        
        public static List<Material> FindLiltoonMaterials()
        {
            var materials = new List<Material>();
            
            string[] guids = AssetDatabase.FindAssets("t:Material");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (IsLiltoonMaterial(material))
                {
                    materials.Add(material);
                }
            }
            
            return materials;
        }
        
        public static Dictionary<string, object> GetMaterialPropertyValues(Material material)
        {
            var properties = new Dictionary<string, object>();
            
            if (!IsLiltoonMaterial(material))
                return properties;
            
            var shader = material.shader;
            int propertyCount = ShaderUtil.GetPropertyCount(shader);
            
            for (int i = 0; i < propertyCount; i++)
            {
                string propertyName = ShaderUtil.GetPropertyName(shader, i);
                var propertyType = ShaderUtil.GetPropertyType(shader, i);
                
                try
                {
                    switch (propertyType)
                    {
                        case ShaderUtil.ShaderPropertyType.Color:
                            if (material.HasProperty(propertyName))
                                properties[propertyName] = material.GetColor(propertyName);
                            break;
                            
                        case ShaderUtil.ShaderPropertyType.Float:
                        case ShaderUtil.ShaderPropertyType.Range:
                            if (material.HasProperty(propertyName))
                                properties[propertyName] = material.GetFloat(propertyName);
                            break;
                            
                        case ShaderUtil.ShaderPropertyType.Vector:
                            if (material.HasProperty(propertyName))
                                properties[propertyName] = material.GetVector(propertyName);
                            break;
                            
                        case ShaderUtil.ShaderPropertyType.TexEnv:
                            if (material.HasProperty(propertyName))
                                properties[propertyName] = material.GetTexture(propertyName);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to get property {propertyName}: {e.Message}");
                }
            }
            
            return properties;
        }
    }
}