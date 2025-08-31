using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace TexColorAdjusterNamespace
{
    // Material Unify Tool methods for TexColAdjuster integration
    public static class MaterialUnifyToolMethods
    {
        // Transfer categories enum (moved from TexColAdjusterWindow)
        [System.Flags]
        public enum TransferCategories
        {
            None = 0,
            ライティング設定 = 1,
            影 = 2,
            発光テクスチャ = 4,
            発光テクスチャ2nd = 8,
            sRimShade = 16,
            逆光ライト = 32,
            反射 = 64,
            マットキャップ = 128,
            マットキャップ2nd = 256,
            リムライト = 512,
            距離フェード = 1024,
            輪郭線 = 2048
        }
        
        // Transfer selected categories
        public static void TransferSelectedCategories(Material sourceMaterial, List<Material> targetMaterials, 
            TransferCategories selectedCategories,
            bool transferReflectionCubeTex, bool transferMatCapTex, bool transferMatCapBumpMask,
            bool transferMatCap2ndTex, bool transferMatCap2ndBumpMask)
        {
            if (sourceMaterial == null || targetMaterials == null || targetMaterials.Count == 0)
            {
                EditorUtility.DisplayDialog("エラー", "転送元または転送先マテリアルが選択されていません。", "OK");
                return;
            }

            try
            {
                int totalTransferCount = 0;
                var transferResults = new List<string>();
                
                if (!IsLiltoonMaterial(sourceMaterial))
                {
                    transferResults.Add($"⚠️ {sourceMaterial.name} (転送元): liltoonではありません");
                }
                else
                {
                    foreach (var targetMaterial in targetMaterials)
                    {
                        if (!IsLiltoonMaterial(targetMaterial))
                        {
                            transferResults.Add($"⚠️ {targetMaterial.name} (転送先): liltoonではありません");
                            continue;
                        }
                        
                        Undo.RecordObject(targetMaterial, "Material Unify Tool - Transfer Properties");
                        
                        int transferCount = 0;
                        transferCount += TransferCategoryProperties(sourceMaterial, targetMaterial, selectedCategories);
                        transferCount += TransferTextures(sourceMaterial, targetMaterial,
                            transferReflectionCubeTex, transferMatCapTex, transferMatCapBumpMask,
                            transferMatCap2ndTex, transferMatCap2ndBumpMask);
                        
                        if (transferCount > 0)
                        {
                            EditorUtility.SetDirty(targetMaterial);
                            transferResults.Add($"✓ {sourceMaterial.name} → {targetMaterial.name}: {transferCount}個");
                            totalTransferCount += transferCount;
                        }
                        else
                        {
                            transferResults.Add($"- {sourceMaterial.name} → {targetMaterial.name}: 転送可能な項目なし");
                        }
                    }
                }

                // Show results
                var resultMessage = $"転送完了: {totalTransferCount}個のプロパティ\n\n" +
                                  string.Join("\n", transferResults);
                
                EditorUtility.DisplayDialog("転送結果", resultMessage, "OK");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("エラー", $"転送中にエラーが発生しました: {e.Message}", "OK");
            }
        }

        private static int TransferCategoryProperties(Material sourceMaterial, Material targetMaterial,
            TransferCategories selectedCategories)
        {
            int count = 0;
            
            if ((selectedCategories & TransferCategories.ライティング設定) != 0)
                count += TransferProperties(sourceMaterial, targetMaterial, MaterialUnifyToolProperties.LightingProperties);
                
            if ((selectedCategories & TransferCategories.影) != 0)
                count += TransferProperties(sourceMaterial, targetMaterial, MaterialUnifyToolProperties.ShadowProperties);
                
            if ((selectedCategories & TransferCategories.発光テクスチャ) != 0)
                count += TransferProperties(sourceMaterial, targetMaterial, MaterialUnifyToolProperties.EmissionProperties);
                
            if ((selectedCategories & TransferCategories.発光テクスチャ2nd) != 0)
                count += TransferProperties(sourceMaterial, targetMaterial, MaterialUnifyToolProperties.Emission2ndProperties);
                
            if ((selectedCategories & TransferCategories.sRimShade) != 0)
                count += TransferProperties(sourceMaterial, targetMaterial, MaterialUnifyToolProperties.sRimShadeProperties);
                
            if ((selectedCategories & TransferCategories.逆光ライト) != 0)
                count += TransferProperties(sourceMaterial, targetMaterial, MaterialUnifyToolProperties.BacklightProperties);
                
            if ((selectedCategories & TransferCategories.反射) != 0)
                count += TransferProperties(sourceMaterial, targetMaterial, MaterialUnifyToolProperties.ReflectionProperties);
                
            if ((selectedCategories & TransferCategories.マットキャップ) != 0)
                count += TransferProperties(sourceMaterial, targetMaterial, MaterialUnifyToolProperties.MatCapProperties);
                
            if ((selectedCategories & TransferCategories.マットキャップ2nd) != 0)
                count += TransferProperties(sourceMaterial, targetMaterial, MaterialUnifyToolProperties.MatCap2ndProperties);
                
            if ((selectedCategories & TransferCategories.リムライト) != 0)
                count += TransferProperties(sourceMaterial, targetMaterial, MaterialUnifyToolProperties.RimLightProperties);
                
            if ((selectedCategories & TransferCategories.距離フェード) != 0)
                count += TransferProperties(sourceMaterial, targetMaterial, MaterialUnifyToolProperties.DistanceFadeProperties);
                
            if ((selectedCategories & TransferCategories.輪郭線) != 0)
                count += TransferProperties(sourceMaterial, targetMaterial, MaterialUnifyToolProperties.OutlineProperties);
                
            return count;
        }

        private static int TransferProperties(Material sourceMaterial, Material targetMaterial, string[] propertyNames)
        {
            int count = 0;
            
            foreach (string propertyName in propertyNames)
            {
                if (sourceMaterial.HasProperty(propertyName) && targetMaterial.HasProperty(propertyName))
                {
                    if (TryGetPropertyType(sourceMaterial, propertyName, out var propType))
                    {
                        switch (propType)
                        {
                            case MaterialProperty.PropType.Color:
                                targetMaterial.SetColor(propertyName, sourceMaterial.GetColor(propertyName));
                                count++;
                                break;
                            case MaterialProperty.PropType.Float:
                            case MaterialProperty.PropType.Range:
                                targetMaterial.SetFloat(propertyName, sourceMaterial.GetFloat(propertyName));
                                count++;
                                break;
                            case MaterialProperty.PropType.Vector:
                                targetMaterial.SetVector(propertyName, sourceMaterial.GetVector(propertyName));
                                count++;
                                break;
                            case MaterialProperty.PropType.Texture:
                                targetMaterial.SetTexture(propertyName, sourceMaterial.GetTexture(propertyName));
                                count++;
                                break;
                        }
                    }
                }
            }
            
            return count;
        }

        private static bool TryGetPropertyType(Material material, string propertyName, out MaterialProperty.PropType propType)
        {
            propType = MaterialProperty.PropType.Float;
            
            try
            {
                if (material.HasProperty(propertyName))
                {
                    var shader = material.shader;
                    int propertyIndex = -1;
                    
                    for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
                    {
                        if (ShaderUtil.GetPropertyName(shader, i) == propertyName)
                        {
                            propertyIndex = i;
                            break;
                        }
                    }
                    
                    if (propertyIndex >= 0)
                    {
                        var shaderPropType = ShaderUtil.GetPropertyType(shader, propertyIndex);
                        propType = ConvertShaderPropertyType(shaderPropType);
                        return true;
                    }
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static MaterialProperty.PropType ConvertShaderPropertyType(ShaderUtil.ShaderPropertyType shaderPropType)
        {
            switch (shaderPropType)
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    return MaterialProperty.PropType.Color;
                case ShaderUtil.ShaderPropertyType.Vector:
                    return MaterialProperty.PropType.Vector;
                case ShaderUtil.ShaderPropertyType.Float:
                    return MaterialProperty.PropType.Float;
                case ShaderUtil.ShaderPropertyType.Range:
                    return MaterialProperty.PropType.Range;
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    return MaterialProperty.PropType.Texture;
                default:
                    return MaterialProperty.PropType.Float;
            }
        }

        private static int TransferTextures(Material sourceMaterial, Material targetMaterial,
            bool transferReflectionCubeTex, bool transferMatCapTex, bool transferMatCapBumpMask,
            bool transferMatCap2ndTex, bool transferMatCap2ndBumpMask)
        {
            int count = 0;
            
            if (transferReflectionCubeTex && sourceMaterial.HasProperty("_ReflectionCubeTex") && 
                targetMaterial.HasProperty("_ReflectionCubeTex"))
            {
                targetMaterial.SetTexture("_ReflectionCubeTex", sourceMaterial.GetTexture("_ReflectionCubeTex"));
                count++;
            }
            
            if (transferMatCapTex && sourceMaterial.HasProperty("_MatCapTex") && 
                targetMaterial.HasProperty("_MatCapTex"))
            {
                targetMaterial.SetTexture("_MatCapTex", sourceMaterial.GetTexture("_MatCapTex"));
                count++;
            }
            
            if (transferMatCapBumpMask && sourceMaterial.HasProperty("_MatCapBumpMask") && 
                targetMaterial.HasProperty("_MatCapBumpMask"))
            {
                targetMaterial.SetTexture("_MatCapBumpMask", sourceMaterial.GetTexture("_MatCapBumpMask"));
                count++;
            }
            
            if (transferMatCap2ndTex && sourceMaterial.HasProperty("_MatCap2ndTex") && 
                targetMaterial.HasProperty("_MatCap2ndTex"))
            {
                targetMaterial.SetTexture("_MatCap2ndTex", sourceMaterial.GetTexture("_MatCap2ndTex"));
                count++;
            }
            
            if (transferMatCap2ndBumpMask && sourceMaterial.HasProperty("_MatCap2ndBumpMask") && 
                targetMaterial.HasProperty("_MatCap2ndBumpMask"))
            {
                targetMaterial.SetTexture("_MatCap2ndBumpMask", sourceMaterial.GetTexture("_MatCap2ndBumpMask"));
                count++;
            }
            
            return count;
        }
        
        private static readonly string[] LiltoonShaderNames = {
            "lilToon",
            "Hidden/lilToonOutline",
            "Hidden/lilToonCutout",
            "Hidden/lilToonTransparent",
            "Hidden/lilToonOnePass",
            "Hidden/lilToonTwoPass"
        };
        
        private static bool IsLiltoonShader(Shader shader)
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
    }
}