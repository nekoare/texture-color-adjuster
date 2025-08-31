using System;
using System.Collections.Generic;
using UnityEngine;

namespace TexColAdjuster
{
    [Serializable]
    public class LiltoonPreset : ScriptableObject
    {
        [Header("Basic Information")]
        public string presetName = "New Preset";
        public string description = "";
        public string sourceShaderName = "";
        
        [Header("Color Settings")]
        public Dictionary<string, Color> colors = new Dictionary<string, Color>();
        
        [Header("Float Settings")]
        public Dictionary<string, float> floats = new Dictionary<string, float>();
        
        [Header("Vector Settings")]
        public Dictionary<string, Vector4> vectors = new Dictionary<string, Vector4>();
        
        [Header("Texture References")]
        public Dictionary<string, string> textureGuids = new Dictionary<string, string>();
        
        // Serializable versions for Unity serialization
        [SerializeField] private List<ColorProperty> serializedColors = new List<ColorProperty>();
        [SerializeField] private List<FloatProperty> serializedFloats = new List<FloatProperty>();
        [SerializeField] private List<VectorProperty> serializedVectors = new List<VectorProperty>();
        [SerializeField] private List<TextureProperty> serializedTextures = new List<TextureProperty>();
        
        [Serializable]
        public class ColorProperty
        {
            public string name;
            public Color value;
            
            public ColorProperty(string name, Color value)
            {
                this.name = name;
                this.value = value;
            }
        }
        
        [Serializable]
        public class FloatProperty
        {
            public string name;
            public float value;
            
            public FloatProperty(string name, float value)
            {
                this.name = name;
                this.value = value;
            }
        }
        
        [Serializable]
        public class VectorProperty
        {
            public string name;
            public Vector4 value;
            
            public VectorProperty(string name, Vector4 value)
            {
                this.name = name;
                this.value = value;
            }
        }
        
        [Serializable]
        public class TextureProperty
        {
            public string name;
            public string guid;
            
            public TextureProperty(string name, string guid)
            {
                this.name = name;
                this.guid = guid;
            }
        }
        
        private void OnEnable()
        {
            // Convert serialized data to dictionaries
            colors.Clear();
            foreach (var prop in serializedColors)
                colors[prop.name] = prop.value;
                
            floats.Clear();
            foreach (var prop in serializedFloats)
                floats[prop.name] = prop.value;
                
            vectors.Clear();
            foreach (var prop in serializedVectors)
                vectors[prop.name] = prop.value;
                
            textureGuids.Clear();
            foreach (var prop in serializedTextures)
                textureGuids[prop.name] = prop.guid;
        }
        
        public void UpdateSerializedData()
        {
            // Convert dictionaries to serialized data
            serializedColors.Clear();
            foreach (var kvp in colors)
                serializedColors.Add(new ColorProperty(kvp.Key, kvp.Value));
                
            serializedFloats.Clear();
            foreach (var kvp in floats)
                serializedFloats.Add(new FloatProperty(kvp.Key, kvp.Value));
                
            serializedVectors.Clear();
            foreach (var kvp in vectors)
                serializedVectors.Add(new VectorProperty(kvp.Key, kvp.Value));
                
            serializedTextures.Clear();
            foreach (var kvp in textureGuids)
                serializedTextures.Add(new TextureProperty(kvp.Key, kvp.Value));
        }
        
        public void SetColor(string propertyName, Color color)
        {
            colors[propertyName] = color;
        }
        
        public void SetFloat(string propertyName, float value)
        {
            floats[propertyName] = value;
        }
        
        public void SetVector(string propertyName, Vector4 vector)
        {
            vectors[propertyName] = vector;
        }
        
        public void SetTextureGuid(string propertyName, string guid)
        {
            textureGuids[propertyName] = guid;
        }
        
        public bool HasProperty(string propertyName)
        {
            return colors.ContainsKey(propertyName) || 
                   floats.ContainsKey(propertyName) || 
                   vectors.ContainsKey(propertyName) || 
                   textureGuids.ContainsKey(propertyName);
        }
        
        public void ClearAllProperties()
        {
            colors.Clear();
            floats.Clear();
            vectors.Clear();
            textureGuids.Clear();
            UpdateSerializedData();
        }
        
        public int GetPropertyCount()
        {
            return colors.Count + floats.Count + vectors.Count + textureGuids.Count;
        }
        
        public Color GetColor(string propertyName, Color defaultValue = default)
        {
            return colors.TryGetValue(propertyName, out Color value) ? value : defaultValue;
        }
        
        public float GetFloat(string propertyName, float defaultValue = 0f)
        {
            return floats.TryGetValue(propertyName, out float value) ? value : defaultValue;
        }
        
        public Vector4 GetVector(string propertyName, Vector4 defaultValue = default)
        {
            return vectors.TryGetValue(propertyName, out Vector4 value) ? value : defaultValue;
        }
        
        public string GetTextureGuid(string propertyName, string defaultValue = "")
        {
            return textureGuids.TryGetValue(propertyName, out string value) ? value : defaultValue;
        }
    }
    
    [System.Flags]
    public enum PresetApplyFlags
    {
        None = 0,
        MainSettings = 1,
        Shadow = 2,
        Shadow2nd = 4,
        Shadow3rd = 8,
        Rim = 16,
        Backlight = 32,
        Emission = 64,
        MatCap = 128,
        Outline = 256,
        Lighting = 512,
        sRimShade = 1024,
        RenderingSettings = 2048,
        All = MainSettings | Shadow | Shadow2nd | Shadow3rd | Rim | Backlight | Emission | MatCap | Outline | Lighting | sRimShade | RenderingSettings,
        DrawingEffects = Shadow | Shadow2nd | Shadow3rd | sRimShade | Backlight | Rim | Lighting
    }
    
    public static class LiltoonPropertyNames
    {
        // Main Colors
        public const string MainColor = "_Color";
        public const string MainTex = "_MainTex";
        
        // Shadow Settings
        public const string UseShadow = "_UseShadow";
        public const string ShadowColor = "_ShadowColor";
        public const string ShadowBorder = "_ShadowBorder";
        public const string ShadowBlur = "_ShadowBlur";
        public const string ShadowStrength = "_ShadowStrength";
        public const string ShadowBorderColor = "_ShadowBorderColor";
        public const string ShadowBorderRange = "_ShadowBorderRange";
        
        // 2nd Shadow
        public const string Shadow2ndColor = "_Shadow2ndColor";
        public const string Shadow2ndBorder = "_Shadow2ndBorder";
        public const string Shadow2ndBlur = "_Shadow2ndBlur";
        public const string Shadow2ndNormalStrength = "_Shadow2ndNormalStrength";
        public const string Shadow2ndReceive = "_Shadow2ndReceive";
        public const string Shadow2ndColorTex = "_Shadow2ndColorTex";
        
        // 3rd Shadow
        public const string Shadow3rdColor = "_Shadow3rdColor";
        public const string Shadow3rdBorder = "_Shadow3rdBorder";
        public const string Shadow3rdBlur = "_Shadow3rdBlur";
        public const string Shadow3rdNormalStrength = "_Shadow3rdNormalStrength";
        public const string Shadow3rdReceive = "_Shadow3rdReceive";
        public const string Shadow3rdColorTex = "_Shadow3rdColorTex";
        
        // Rim Light
        public const string UseRim = "_UseRim";
        public const string RimColor = "_RimColor";
        public const string RimBorder = "_RimBorder";
        public const string RimBlur = "_RimBlur";
        public const string RimFresnelPower = "_RimFresnelPower";
        public const string RimEnableLighting = "_RimEnableLighting";
        public const string RimShadowMask = "_RimShadowMask";
        
        // Backlight
        public const string UseBacklight = "_UseBacklight";
        public const string BacklightColor = "_BacklightColor";
        public const string BacklightMainStrength = "_BacklightMainStrength";
        public const string BacklightNormalStrength = "_BacklightNormalStrength";
        public const string BacklightBorder = "_BacklightBorder";
        public const string BacklightBlur = "_BacklightBlur";
        
        // sRimShade
        public const string UsesRimShade = "_UsesRimShade";
        public const string sRimShadeColor = "_sRimShadeColor";
        public const string sRimShadeBorder = "_sRimShadeBorder";
        public const string sRimShadeBlur = "_sRimShadeBlur";
        public const string sRimShadeNormalStrength = "_sRimShadeNormalStrength";
        
        // Lighting & Brightness
        public const string LightMinLimit = "_LightMinLimit";
        public const string LightMaxLimit = "_LightMaxLimit";
        public const string MonochromeLighting = "_MonochromeLighting";
        public const string AsUnlit = "_AsUnlit";
        public const string VertexLightStrength = "_VertexLightStrength";
        public const string LightDirectionOverride = "_LightDirectionOverride";
        public const string AlphaBoostFA = "_AlphaBoostFA";
        
        // Rendering Settings
        public const string Cull = "_Cull";
        public const string ZClip = "_ZClip";
        public const string ZWrite = "_ZWrite";
        public const string ZTest = "_ZTest";
        public const string OffsetFactor = "_OffsetFactor";
        public const string OffsetUnits = "_OffsetUnits";
        public const string ColorMask = "_ColorMask";
        public const string AlphaToMask = "_AlphaToMask";
        
        // Emission
        public const string UseEmission = "_UseEmission";
        public const string EmissionColor = "_EmissionColor";
        public const string EmissionMap = "_EmissionMap";
        public const string EmissionBlink = "_EmissionBlink";
        
        // Outline
        public const string OutlineWidth = "_OutlineWidth";
        public const string OutlineColor = "_OutlineColor";
        public const string OutlineTex = "_OutlineTex";
        public const string OutlineTexHSVG = "_OutlineTexHSVG";
        
        public static readonly string[] ShadowProperties = {
            UseShadow, ShadowColor, ShadowBorder, ShadowBlur, ShadowStrength,
            ShadowBorderColor, ShadowBorderRange
        };
        
        public static readonly string[] Shadow2ndProperties = {
            Shadow2ndColor, Shadow2ndBorder, Shadow2ndBlur,
            Shadow2ndNormalStrength, Shadow2ndReceive, Shadow2ndColorTex
        };
        
        public static readonly string[] Shadow3rdProperties = {
            Shadow3rdColor, Shadow3rdBorder, Shadow3rdBlur,
            Shadow3rdNormalStrength, Shadow3rdReceive, Shadow3rdColorTex
        };
        
        public static readonly string[] RimProperties = {
            UseRim, RimColor, RimBorder, RimBlur, RimFresnelPower,
            RimEnableLighting, RimShadowMask
        };
        
        public static readonly string[] BacklightProperties = {
            UseBacklight, BacklightColor, BacklightMainStrength, BacklightNormalStrength,
            BacklightBorder, BacklightBlur
        };
        
        public static readonly string[] EmissionProperties = {
            UseEmission, EmissionColor, EmissionMap, EmissionBlink
        };
        
        public static readonly string[] OutlineProperties = {
            OutlineWidth, OutlineColor, OutlineTex, OutlineTexHSVG
        };
        
        public static readonly string[] MainProperties = {
            MainColor, MainTex
        };
        
        public static readonly string[] sRimShadeProperties = {
            UsesRimShade, sRimShadeColor, sRimShadeBorder, sRimShadeBlur, sRimShadeNormalStrength
        };
        
        public static readonly string[] LightingProperties = {
            LightMinLimit, LightMaxLimit, MonochromeLighting, AsUnlit, VertexLightStrength,
            LightDirectionOverride, AlphaBoostFA
        };
        
        public static readonly string[] RenderingProperties = {
            Cull, ZClip, ZWrite, ZTest, OffsetFactor, OffsetUnits, ColorMask, AlphaToMask
        };
        
        // Combined property sets for Preset_Sample specification
        public static readonly string[] DrawingEffectProperties = {
            // Lighting & Brightness
            LightMinLimit, LightMaxLimit, MonochromeLighting, AsUnlit, VertexLightStrength,
            LightDirectionOverride, AlphaBoostFA,
            // Shadow
            UseShadow, ShadowColor, ShadowBorder, ShadowBlur, ShadowStrength,
            ShadowBorderColor, ShadowBorderRange,
            // Shadow2nd
            Shadow2ndColor, Shadow2ndBorder, Shadow2ndBlur,
            Shadow2ndNormalStrength, Shadow2ndReceive, Shadow2ndColorTex,
            // Shadow3rd
            Shadow3rdColor, Shadow3rdBorder, Shadow3rdBlur,
            Shadow3rdNormalStrength, Shadow3rdReceive, Shadow3rdColorTex,
            // sRimShade
            UsesRimShade, sRimShadeColor, sRimShadeBorder, sRimShadeBlur, sRimShadeNormalStrength,
            // Backlight
            UseBacklight, BacklightColor, BacklightMainStrength, BacklightNormalStrength,
            BacklightBorder, BacklightBlur,
            // Rim
            UseRim, RimColor, RimBorder, RimBlur, RimFresnelPower,
            RimEnableLighting, RimShadowMask
        };
    }
}