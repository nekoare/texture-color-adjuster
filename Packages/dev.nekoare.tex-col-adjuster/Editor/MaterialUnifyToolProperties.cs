using System;

namespace TexColorAdjusterNamespace
{
    public static class MaterialUnifyToolProperties
    {
        // ライティング設定
        public static readonly string[] LightingProperties = {
            "_LightMinLimit",
            "_LightMaxLimit", 
            "_MonochromeLighting",
            "_AsUnlit",
            "_VertexLightStrength",
            "_LightDirectionOverride",
            "_AlphaBoostFA"
        };

        // 影設定
        public static readonly string[] ShadowProperties = {
            "_UseShadow",
            "_ShadowColor",
            "_ShadowBorder",
            "_ShadowBlur", 
            "_ShadowStrength",
            "_ShadowBorderColor",
            "_ShadowBorderRange",
            "_ShadowColorTex",
            "_ShadowNormalStrength",
            "_ShadowReceive"
        };

        // 発光テクスチャ
        public static readonly string[] EmissionProperties = {
            "_UseEmission",
            "_EmissionColor", 
            "_EmissionMap",
            "_EmissionBlink",
            "_EmissionParallaxDepth",
            "_EmissionFluorescence"
        };

        // 発光テクスチャ2nd
        public static readonly string[] Emission2ndProperties = {
            "_UseEmission2nd",
            "_Emission2ndColor",
            "_Emission2ndMap", 
            "_Emission2ndMap_ScrollRotate",
            "_Emission2ndBlink",
            "_Emission2ndParallaxDepth",
            "_Emission2ndFluorescence"
        };

        // sRimShade
        public static readonly string[] sRimShadeProperties = {
            "_UsesRimShade",
            "_sRimShadeColor",
            "_sRimShadeBorder", 
            "_sRimShadeBlur",
            "_sRimShadeNormalStrength"
        };

        // 逆光ライト
        public static readonly string[] BacklightProperties = {
            "_UseBacklight",
            "_BacklightColor",
            "_BacklightMainStrength",
            "_BacklightNormalStrength",
            "_BacklightBorder",
            "_BacklightBlur"
        };

        // 反射
        public static readonly string[] ReflectionProperties = {
            "_UseReflection",
            "_ReflectionColor",
            "_ReflectionCubeTex",
            "_ReflectionStrength",
            "_ReflectionNormalStrength",
            "_ReflectionBlendMode",
            "_ReflectionApplyTransparency"
        };

        // マットキャップ
        public static readonly string[] MatCapProperties = {
            "_UseMatCap", 
            "_MatCapTex",
            "_MatCapColor",
            "_MatCapMul",
            "_MatCapAdditivity",
            "_MatCapBumpScale",
            "_MatCapBumpMask",
            "_MatCapBlendUV1",
            "_MatCapZRotCancel",
            "_MatCapPerspective",
            "_MatCapVRParallaxStrength"
        };

        // マットキャップ2nd
        public static readonly string[] MatCap2ndProperties = {
            "_UseMatCap2nd",
            "_MatCap2ndTex", 
            "_MatCap2ndColor",
            "_MatCap2ndMul",
            "_MatCap2ndAdditivity",
            "_MatCap2ndBumpScale",
            "_MatCap2ndBumpMask",
            "_MatCap2ndBlendUV1",
            "_MatCap2ndZRotCancel",
            "_MatCap2ndPerspective",
            "_MatCap2ndVRParallaxStrength"
        };

        // リムライト
        public static readonly string[] RimLightProperties = {
            "_UseRim",
            "_RimColor",
            "_RimBorder",
            "_RimBlur",
            "_RimFresnelPower",
            "_RimEnableLighting",
            "_RimShadowMask",
            "_RimApplyTransparency",
            "_RimDirRange",
            "_RimDirStrength",
            "_RimIndirRange",
            "_RimIndirColor",
            "_RimIndirBorder"
        };

        // 距離フェード
        public static readonly string[] DistanceFadeProperties = {
            "_UseDistanceFade",
            "_DistanceFade",
            "_DistanceFadeColor",
            "_DistanceFadeMode",
            "_DistanceFadeRim"
        };

        // 輪郭線
        public static readonly string[] OutlineProperties = {
            "_OutlineWidth",
            "_OutlineColor",
            "_OutlineTex", 
            "_OutlineTexHSVG",
            "_OutlineLitColor",
            "_OutlineLitApplyTex",
            "_OutlineLitScale",
            "_OutlineEnableLighting",
            "_OutlineDeleteMesh",
            "_OutlineFixWidth",
            "_OutlineVertexR2Width"
        };

        // すべての転送可能プロパティを組み合わせたもの
        public static readonly string[] AllTransferableProperties = CombineArrays(
            LightingProperties,
            ShadowProperties, 
            EmissionProperties,
            Emission2ndProperties,
            sRimShadeProperties,
            BacklightProperties,
            ReflectionProperties,
            MatCapProperties,
            MatCap2ndProperties,
            RimLightProperties,
            DistanceFadeProperties,
            OutlineProperties
        );

        // 配列を結合するヘルパーメソッド
        private static string[] CombineArrays(params string[][] arrays)
        {
            int totalLength = 0;
            foreach (var array in arrays)
            {
                totalLength += array.Length;
            }

            string[] result = new string[totalLength];
            int index = 0;

            foreach (var array in arrays)
            {
                Array.Copy(array, 0, result, index, array.Length);
                index += array.Length;
            }

            return result;
        }

        // 特定のテクスチャプロパティ（別途転送オプション用）
        public static readonly string[] TextureTransferProperties = {
            "_ReflectionCubeTex",
            "_MatCapTex",
            "_MatCapBumpMask", 
            "_MatCap2ndTex",
            "_MatCap2ndBumpMask"
        };
    }
}