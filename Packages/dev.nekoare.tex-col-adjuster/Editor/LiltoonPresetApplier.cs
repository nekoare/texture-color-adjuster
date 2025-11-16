using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace TexColAdjuster.Editor
{
    public static class LiltoonPresetApplier
    {
        public static void ApplyPresetToMaterial(Material targetMaterial, LiltoonPreset preset, float intensity = 1.0f, PresetApplyFlags flags = PresetApplyFlags.All)
        {
            if (!LiltoonPresetReader.IsLiltoonMaterial(targetMaterial) || preset == null)
            {
                Debug.LogError("Target material is not using liltoon shader or preset is null");
                return;
            }

            Undo.RecordObject(targetMaterial, $"Apply Liltoon Preset: {preset.presetName}");

            try
            {
                ApplyColorProperties(targetMaterial, preset, intensity, flags);
                ApplyFloatProperties(targetMaterial, preset, intensity, flags);
                ApplyVectorProperties(targetMaterial, preset, intensity, flags);
                ApplyTextureProperties(targetMaterial, preset, flags);
                
                EditorUtility.SetDirty(targetMaterial);
                Debug.Log($"Applied preset '{preset.presetName}' to material '{targetMaterial.name}' with intensity {intensity:F2}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to apply preset to material: {e.Message}");
            }
        }

        public static void ApplyPresetToMaterials(List<Material> materials, LiltoonPreset preset, float intensity = 1.0f, PresetApplyFlags flags = PresetApplyFlags.All)
        {
            if (materials == null || preset == null)
            {
                Debug.LogError("Materials list or preset is null");
                return;
            }

            foreach (var material in materials)
            {
                if (LiltoonPresetReader.IsLiltoonMaterial(material))
                {
                    ApplyPresetToMaterial(material, preset, intensity, flags);
                }
            }
        }

        public static void ApplyPresetSelectively(Material targetMaterial, LiltoonPreset preset, string[] propertyNames, float intensity = 1.0f)
        {
            if (!LiltoonPresetReader.IsLiltoonMaterial(targetMaterial) || preset == null || propertyNames == null)
            {
                Debug.LogError("Invalid parameters for selective preset application");
                return;
            }

            Undo.RecordObject(targetMaterial, $"Apply Preset Properties: {string.Join(", ", propertyNames)}");

            try
            {
                foreach (string propertyName in propertyNames)
                {
                    if (preset.colors.ContainsKey(propertyName))
                    {
                        ApplyColorProperty(targetMaterial, propertyName, preset.colors[propertyName], intensity);
                    }
                    else if (preset.floats.ContainsKey(propertyName))
                    {
                        ApplyFloatProperty(targetMaterial, propertyName, preset.floats[propertyName], intensity);
                    }
                    else if (preset.vectors.ContainsKey(propertyName))
                    {
                        ApplyVectorProperty(targetMaterial, propertyName, preset.vectors[propertyName], intensity);
                    }
                    else if (preset.textureGuids.ContainsKey(propertyName))
                    {
                        ApplyTextureProperty(targetMaterial, propertyName, preset.textureGuids[propertyName]);
                    }
                }
                
                EditorUtility.SetDirty(targetMaterial);
                Debug.Log($"Applied {propertyNames.Length} properties from preset '{preset.presetName}' to material '{targetMaterial.name}'");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to apply preset properties: {e.Message}");
            }
        }

        private static void ApplyColorProperties(Material material, LiltoonPreset preset, float intensity, PresetApplyFlags flags)
        {
            foreach (var colorProperty in preset.colors)
            {
                if (ShouldApplyProperty(colorProperty.Key, flags))
                {
                    ApplyColorProperty(material, colorProperty.Key, colorProperty.Value, intensity);
                }
            }
        }

        private static void ApplyFloatProperties(Material material, LiltoonPreset preset, float intensity, PresetApplyFlags flags)
        {
            foreach (var floatProperty in preset.floats)
            {
                if (ShouldApplyProperty(floatProperty.Key, flags))
                {
                    ApplyFloatProperty(material, floatProperty.Key, floatProperty.Value, intensity);
                }
            }
        }

        private static void ApplyVectorProperties(Material material, LiltoonPreset preset, float intensity, PresetApplyFlags flags)
        {
            foreach (var vectorProperty in preset.vectors)
            {
                if (ShouldApplyProperty(vectorProperty.Key, flags))
                {
                    ApplyVectorProperty(material, vectorProperty.Key, vectorProperty.Value, intensity);
                }
            }
        }

        private static void ApplyTextureProperties(Material material, LiltoonPreset preset, PresetApplyFlags flags)
        {
            foreach (var textureProperty in preset.textureGuids)
            {
                if (ShouldApplyProperty(textureProperty.Key, flags))
                {
                    ApplyTextureProperty(material, textureProperty.Key, textureProperty.Value);
                }
            }
        }

        private static void ApplyColorProperty(Material material, string propertyName, Color presetValue, float intensity)
        {
            if (!material.HasProperty(propertyName)) return;

            Color currentValue = material.GetColor(propertyName);
            Color blendedValue = Color.Lerp(currentValue, presetValue, intensity);
            material.SetColor(propertyName, blendedValue);
        }

        private static void ApplyFloatProperty(Material material, string propertyName, float presetValue, float intensity)
        {
            if (!material.HasProperty(propertyName)) return;

            float currentValue = material.GetFloat(propertyName);
            float blendedValue = Mathf.Lerp(currentValue, presetValue, intensity);
            material.SetFloat(propertyName, blendedValue);
        }

        private static void ApplyVectorProperty(Material material, string propertyName, Vector4 presetValue, float intensity)
        {
            if (!material.HasProperty(propertyName)) return;

            Vector4 currentValue = material.GetVector(propertyName);
            Vector4 blendedValue = Vector4.Lerp(currentValue, presetValue, intensity);
            material.SetVector(propertyName, blendedValue);
        }

        private static void ApplyTextureProperty(Material material, string propertyName, string textureGuid)
        {
            if (!material.HasProperty(propertyName) || string.IsNullOrEmpty(textureGuid)) return;

            string assetPath = AssetDatabase.GUIDToAssetPath(textureGuid);
            if (!string.IsNullOrEmpty(assetPath))
            {
                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
                if (texture != null)
                {
                    material.SetTexture(propertyName, texture);
                }
            }
        }

        private static bool ShouldApplyProperty(string propertyName, PresetApplyFlags flags)
        {
            if (flags.HasFlag(PresetApplyFlags.All)) return true;

            if (flags.HasFlag(PresetApplyFlags.MainSettings) && IsMainProperty(propertyName)) return true;
            if (flags.HasFlag(PresetApplyFlags.Shadow) && IsShadowProperty(propertyName)) return true;
            if (flags.HasFlag(PresetApplyFlags.Shadow2nd) && IsShadow2ndProperty(propertyName)) return true;
            if (flags.HasFlag(PresetApplyFlags.Shadow3rd) && IsShadow3rdProperty(propertyName)) return true;
            if (flags.HasFlag(PresetApplyFlags.Rim) && IsRimProperty(propertyName)) return true;
            if (flags.HasFlag(PresetApplyFlags.Backlight) && IsBacklightProperty(propertyName)) return true;
            if (flags.HasFlag(PresetApplyFlags.Emission) && IsEmissionProperty(propertyName)) return true;
            if (flags.HasFlag(PresetApplyFlags.Outline) && IsOutlineProperty(propertyName)) return true;
            if (flags.HasFlag(PresetApplyFlags.Lighting) && IsLightingProperty(propertyName)) return true;
            if (flags.HasFlag(PresetApplyFlags.sRimShade) && IssRimShadeProperty(propertyName)) return true;
            if (flags.HasFlag(PresetApplyFlags.RenderingSettings) && IsRenderingProperty(propertyName)) return true;

            return false;
        }

        private static bool IsMainProperty(string propertyName)
        {
            return Array.Exists(LiltoonPropertyNames.MainProperties, p => p == propertyName);
        }

        private static bool IsShadowProperty(string propertyName)
        {
            return Array.Exists(LiltoonPropertyNames.ShadowProperties, p => p == propertyName);
        }

        private static bool IsShadow2ndProperty(string propertyName)
        {
            return Array.Exists(LiltoonPropertyNames.Shadow2ndProperties, p => p == propertyName);
        }

        private static bool IsShadow3rdProperty(string propertyName)
        {
            return Array.Exists(LiltoonPropertyNames.Shadow3rdProperties, p => p == propertyName);
        }

        private static bool IsRimProperty(string propertyName)
        {
            return Array.Exists(LiltoonPropertyNames.RimProperties, p => p == propertyName);
        }

        private static bool IsBacklightProperty(string propertyName)
        {
            return Array.Exists(LiltoonPropertyNames.BacklightProperties, p => p == propertyName);
        }

        private static bool IsEmissionProperty(string propertyName)
        {
            return Array.Exists(LiltoonPropertyNames.EmissionProperties, p => p == propertyName);
        }

        private static bool IsOutlineProperty(string propertyName)
        {
            return Array.Exists(LiltoonPropertyNames.OutlineProperties, p => p == propertyName);
        }
        
        private static bool IsLightingProperty(string propertyName)
        {
            return Array.Exists(LiltoonPropertyNames.LightingProperties, p => p == propertyName);
        }
        
        private static bool IssRimShadeProperty(string propertyName)
        {
            return Array.Exists(LiltoonPropertyNames.sRimShadeProperties, p => p == propertyName);
        }
        
        private static bool IsRenderingProperty(string propertyName)
        {
            return Array.Exists(LiltoonPropertyNames.RenderingProperties, p => p == propertyName);
        }
        
        public static void TransferDrawingEffects(Material sourceMaterial, Material targetMaterial, float intensity = 1.0f)
        {
            if (!LiltoonPresetReader.IsLiltoonMaterial(sourceMaterial) || !LiltoonPresetReader.IsLiltoonMaterial(targetMaterial))
            {
                Debug.LogError("Both materials must be using liltoon shader");
                return;
            }
            
            var drawingEffectsPreset = LiltoonPresetReader.GenerateDrawingEffectsPreset(sourceMaterial);
            ApplyPresetToMaterial(targetMaterial, drawingEffectsPreset, intensity, PresetApplyFlags.DrawingEffects);
            
            UnityEngine.Object.DestroyImmediate(drawingEffectsPreset);
            
            Debug.Log($"Transferred drawing effects from '{sourceMaterial.name}' to '{targetMaterial.name}' with intensity {intensity:F2}");
        }

        public static void BlendPresets(Material targetMaterial, LiltoonPreset preset1, LiltoonPreset preset2, float blendFactor, PresetApplyFlags flags = PresetApplyFlags.All)
        {
            if (!LiltoonPresetReader.IsLiltoonMaterial(targetMaterial) || preset1 == null || preset2 == null)
            {
                Debug.LogError("Invalid parameters for preset blending");
                return;
            }

            Undo.RecordObject(targetMaterial, $"Blend Presets: {preset1.presetName} + {preset2.presetName}");

            try
            {
                var blendedPreset = CreateBlendedPreset(preset1, preset2, blendFactor);
                ApplyPresetToMaterial(targetMaterial, blendedPreset, 1.0f, flags);
                
                UnityEngine.Object.DestroyImmediate(blendedPreset);
                EditorUtility.SetDirty(targetMaterial);
                
                Debug.Log($"Blended presets '{preset1.presetName}' and '{preset2.presetName}' with factor {blendFactor:F2}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to blend presets: {e.Message}");
            }
        }

        private static LiltoonPreset CreateBlendedPreset(LiltoonPreset preset1, LiltoonPreset preset2, float blendFactor)
        {
            var blendedPreset = ScriptableObject.CreateInstance<LiltoonPreset>();
            blendedPreset.presetName = $"Blended_{preset1.presetName}_{preset2.presetName}";

            // Blend colors
            var allColorKeys = new HashSet<string>(preset1.colors.Keys);
            allColorKeys.UnionWith(preset2.colors.Keys);
            
            foreach (string key in allColorKeys)
            {
                Color color1 = preset1.GetColor(key, Color.white);
                Color color2 = preset2.GetColor(key, Color.white);
                blendedPreset.SetColor(key, Color.Lerp(color1, color2, blendFactor));
            }

            // Blend floats
            var allFloatKeys = new HashSet<string>(preset1.floats.Keys);
            allFloatKeys.UnionWith(preset2.floats.Keys);
            
            foreach (string key in allFloatKeys)
            {
                float value1 = preset1.GetFloat(key, 0f);
                float value2 = preset2.GetFloat(key, 0f);
                blendedPreset.SetFloat(key, Mathf.Lerp(value1, value2, blendFactor));
            }

            // Blend vectors
            var allVectorKeys = new HashSet<string>(preset1.vectors.Keys);
            allVectorKeys.UnionWith(preset2.vectors.Keys);
            
            foreach (string key in allVectorKeys)
            {
                Vector4 vector1 = preset1.GetVector(key, Vector4.zero);
                Vector4 vector2 = preset2.GetVector(key, Vector4.zero);
                blendedPreset.SetVector(key, Vector4.Lerp(vector1, vector2, blendFactor));
            }

            // For textures, use preset2's textures if blendFactor > 0.5, otherwise use preset1's
            var allTextureKeys = new HashSet<string>(preset1.textureGuids.Keys);
            allTextureKeys.UnionWith(preset2.textureGuids.Keys);
            
            foreach (string key in allTextureKeys)
            {
                string guid = blendFactor > 0.5f ? preset2.GetTextureGuid(key) : preset1.GetTextureGuid(key);
                if (!string.IsNullOrEmpty(guid))
                {
                    blendedPreset.SetTextureGuid(key, guid);
                }
            }

            blendedPreset.UpdateSerializedData();
            return blendedPreset;
        }

        public static LiltoonPreset DuplicatePreset(LiltoonPreset original, string newName = "")
        {
            if (original == null) return null;

            var duplicate = ScriptableObject.CreateInstance<LiltoonPreset>();
            duplicate.presetName = string.IsNullOrEmpty(newName) ? original.presetName + "_Copy" : newName;
            duplicate.description = original.description;
            duplicate.sourceShaderName = original.sourceShaderName;

            foreach (var colorProperty in original.colors)
                duplicate.SetColor(colorProperty.Key, colorProperty.Value);

            foreach (var floatProperty in original.floats)
                duplicate.SetFloat(floatProperty.Key, floatProperty.Value);

            foreach (var vectorProperty in original.vectors)
                duplicate.SetVector(vectorProperty.Key, vectorProperty.Value);

            foreach (var textureProperty in original.textureGuids)
                duplicate.SetTextureGuid(textureProperty.Key, textureProperty.Value);

            duplicate.UpdateSerializedData();
            return duplicate;
        }

        public static bool ComparePresets(LiltoonPreset preset1, LiltoonPreset preset2, float tolerance = 0.001f)
        {
            if (preset1 == null || preset2 == null) return false;
            if (preset1 == preset2) return true;

            // Compare colors
            if (preset1.colors.Count != preset2.colors.Count) return false;
            foreach (var colorProperty in preset1.colors)
            {
                if (!preset2.colors.ContainsKey(colorProperty.Key)) return false;
                
                Color color1 = colorProperty.Value;
                Color color2 = preset2.colors[colorProperty.Key];
                
                if (Mathf.Abs(color1.r - color2.r) > tolerance ||
                    Mathf.Abs(color1.g - color2.g) > tolerance ||
                    Mathf.Abs(color1.b - color2.b) > tolerance ||
                    Mathf.Abs(color1.a - color2.a) > tolerance)
                {
                    return false;
                }
            }

            // Compare floats
            if (preset1.floats.Count != preset2.floats.Count) return false;
            foreach (var floatProperty in preset1.floats)
            {
                if (!preset2.floats.ContainsKey(floatProperty.Key)) return false;
                if (Mathf.Abs(floatProperty.Value - preset2.floats[floatProperty.Key]) > tolerance)
                    return false;
            }

            // Compare vectors
            if (preset1.vectors.Count != preset2.vectors.Count) return false;
            foreach (var vectorProperty in preset1.vectors)
            {
                if (!preset2.vectors.ContainsKey(vectorProperty.Key)) return false;
                
                Vector4 vector1 = vectorProperty.Value;
                Vector4 vector2 = preset2.vectors[vectorProperty.Key];
                
                if (Vector4.Distance(vector1, vector2) > tolerance)
                    return false;
            }

            // Compare texture GUIDs
            if (preset1.textureGuids.Count != preset2.textureGuids.Count) return false;
            foreach (var textureProperty in preset1.textureGuids)
            {
                if (!preset2.textureGuids.ContainsKey(textureProperty.Key)) return false;
                if (textureProperty.Value != preset2.textureGuids[textureProperty.Key])
                    return false;
            }

            return true;
        }
    }
}