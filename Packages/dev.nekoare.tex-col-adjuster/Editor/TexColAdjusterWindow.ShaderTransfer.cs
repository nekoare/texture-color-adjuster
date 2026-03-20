using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using TexColAdjuster.Editor;
using TexColAdjuster.Editor.Models;
using TexColorAdjusterNamespace;
using ColorAdjustmentMode = TexColAdjuster.Runtime.ColorAdjustmentMode;

namespace TexColAdjuster
{
    public partial class TexColAdjusterWindow
    {
        private void DrawShaderSettingsTab()
        {
            if (useNewUI)
                DrawShaderSettingsTabNew();
            else
                DrawShaderSettingsTabClassic();
        }

        private void DrawShaderSettingsTabNew()
        {
            // Step 1: Source selection
            DrawShaderTransferStep1_Source();

            GUILayout.Space(10);

            // Step 2: Target selection
            DrawShaderTransferStep2_Target();

            GUILayout.Space(10);

            // Step 3: Transfer settings + execute
            DrawShaderTransferStep3_Execute();
        }

        private void HandleShaderTransferSourceDrop(UnityEngine.Object obj)
        {
            if (obj is Material mat)
            {
                shaderTransferNewSource = obj;
                shaderTransferNewSourceMaterial = mat;
            }
            else if (obj is GameObject go)
            {
                shaderTransferNewSource = obj;
                // Try direct renderer first, then search children
                var renderer = GetRendererComponent(go);
                if (renderer == null)
                {
                    renderer = go.GetComponentInChildren<Renderer>(true);
                }
                if (renderer != null)
                {
                    var materials = ExtractMaterials(renderer);
                    if (materials != null && materials.Length == 1)
                    {
                        shaderTransferNewSourceMaterial = materials[0];
                    }
                    else
                    {
                        shaderTransferNewSourceMaterial = null; // will be selected via thumbnails
                    }
                }
                else
                {
                    shaderTransferNewSourceMaterial = null;
                }
            }
        }

        private void DrawShaderTransferStep1_Source()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("🎨 転送元（参考にしたい方）", EditorStyles.boldLabel);

            var newSource = EditorGUILayout.ObjectField(shaderTransferNewSource, typeof(UnityEngine.Object), true);
            if (newSource != shaderTransferNewSource)
            {
                if (newSource == null)
                {
                    shaderTransferNewSource = null;
                    shaderTransferNewSourceMaterial = null;
                }
                else
                {
                    HandleShaderTransferSourceDrop(newSource);
                }
            }

            // If source is a GameObject with multiple materials, show thumbnail selection
            if (shaderTransferNewSource is GameObject sourceGO)
            {
                var renderer = GetRendererComponent(sourceGO);
                if (renderer == null)
                    renderer = sourceGO.GetComponentInChildren<Renderer>(true);
                if (renderer == null)
                {
                    EditorGUILayout.HelpBox(LocalizationManager.Get("renderer_not_found_short"), MessageType.Warning);
                }
                else
                {
                    var materials = ExtractMaterials(renderer);
                    if (materials != null && materials.Length > 1)
                    {
                        DrawMaterialSelectionThumbnails(materials, ref shaderTransferNewSourceMaterial, null);
                    }
                }
            }

            // Show selected source material info
            if (shaderTransferNewSourceMaterial != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("選択中: " + shaderTransferNewSourceMaterial.name, EditorStyles.boldLabel);
                if (LiltoonPresetReader.IsLiltoonMaterial(shaderTransferNewSourceMaterial))
                {
                    GUI.color = Color.green;
                    EditorGUILayout.LabelField("✓ liltoon", GUILayout.Width(70));
                }
                else
                {
                    GUI.color = Color.red;
                    EditorGUILayout.LabelField("⚠ 非liltoon", GUILayout.Width(70));
                }
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawShaderTransferStep2_Target()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("🎯 転送先（変更する方）", EditorStyles.boldLabel);

            // Drop zone (DragAndDrop area, no None field)
            Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            var dropStyle = new GUIStyle(EditorStyles.helpBox);
            dropStyle.alignment = TextAnchor.MiddleCenter;
            dropStyle.fontSize = 11;
            GUI.Box(dropArea, "MaterialまたはGameObjectをここにドロップ", dropStyle);

            // Handle drag and drop
            Event evt = Event.current;
            if (dropArea.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                }
                else if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        AddShaderTransferTarget(obj);
                    }
                    evt.Use();
                }
            }

            GUILayout.Space(5);

            // Display added materials
            if (shaderTransferNewTargetMaterials.Count > 0)
            {
                int selectedCount = shaderTransferNewTargetMaterials.Count(m => m != null && shaderTransferNewTargetSelected.Contains(m));
                EditorGUILayout.LabelField($"転送先マテリアル ({selectedCount}/{shaderTransferNewTargetMaterials.Count}件選択中)", EditorStyles.miniBoldLabel);

                // Thumbnail grid (click to toggle selection)
                float btnSize = 48f;
                EditorGUILayout.BeginHorizontal();
                for (int i = 0; i < shaderTransferNewTargetMaterials.Count; i++)
                {
                    var mat = shaderTransferNewTargetMaterials[i];
                    if (mat == null) continue;

                    bool isSelected = shaderTransferNewTargetSelected.Contains(mat);
                    bool isLiltoon = LiltoonPresetReader.IsLiltoonMaterial(mat);

                    EditorGUILayout.BeginVertical(GUILayout.Width(btnSize + 4));

                    var prevBg = GUI.backgroundColor;
                    if (isSelected)
                        GUI.backgroundColor = isLiltoon ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.7f, 0.3f);
                    else
                        GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f);

                    var preview = AssetPreview.GetAssetPreview(mat);
                    var content = preview != null
                        ? new GUIContent(preview, mat.name + (isLiltoon ? "" : " ⚠ 非liltoon") + (isSelected ? " (選択中)" : " (未選択)"))
                        : new GUIContent(mat.name);

                    if (GUILayout.Button(content, GUILayout.Width(btnSize), GUILayout.Height(btnSize)))
                    {
                        if (isSelected)
                            shaderTransferNewTargetSelected.Remove(mat);
                        else
                            shaderTransferNewTargetSelected.Add(mat);
                    }

                    GUI.backgroundColor = prevBg;

                    string displayName = mat.name.Length > 8 ? mat.name.Substring(0, 7) + "…" : mat.name;
                    EditorGUILayout.LabelField(displayName, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(btnSize));

                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(5);

                // List view with checkboxes and remove buttons
                int removeIndex = -1;
                for (int i = 0; i < shaderTransferNewTargetMaterials.Count; i++)
                {
                    var mat = shaderTransferNewTargetMaterials[i];
                    if (mat == null) { removeIndex = i; continue; }

                    bool isSelected = shaderTransferNewTargetSelected.Contains(mat);
                    bool isLiltoon = LiltoonPresetReader.IsLiltoonMaterial(mat);

                    EditorGUILayout.BeginHorizontal();

                    // Selection toggle
                    bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                    if (newSelected != isSelected)
                    {
                        if (newSelected) shaderTransferNewTargetSelected.Add(mat);
                        else shaderTransferNewTargetSelected.Remove(mat);
                    }

                    // Material name
                    if (!isLiltoon) GUI.color = Color.yellow;
                    EditorGUILayout.LabelField(mat.name);
                    GUI.color = Color.white;

                    // Liltoon indicator
                    if (isLiltoon)
                    {
                        GUI.color = Color.green;
                        EditorGUILayout.LabelField("✓", GUILayout.Width(15));
                        GUI.color = Color.white;
                    }
                    else
                    {
                        EditorGUILayout.LabelField("⚠", GUILayout.Width(15));
                    }

                    // Remove button
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                    {
                        removeIndex = i;
                    }

                    EditorGUILayout.EndHorizontal();
                }
                if (removeIndex >= 0)
                {
                    var removed = shaderTransferNewTargetMaterials[removeIndex];
                    shaderTransferNewTargetSelected.Remove(removed);
                    shaderTransferNewTargetMaterials.RemoveAt(removeIndex);
                }

                // All/None/Clear buttons
                GUILayout.Space(3);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("全選択", GUILayout.Width(60)))
                {
                    foreach (var m in shaderTransferNewTargetMaterials)
                        if (m != null) shaderTransferNewTargetSelected.Add(m);
                }
                if (GUILayout.Button("全解除", GUILayout.Width(60)))
                {
                    shaderTransferNewTargetSelected.Clear();
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("全てクリア", GUILayout.Width(80)))
                {
                    shaderTransferNewTargetMaterials.Clear();
                    shaderTransferNewTargetSelected.Clear();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("転送先がありません。MaterialまたはGameObjectをドロップしてください。", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void AddShaderTransferTarget(UnityEngine.Object obj)
        {
            if (obj is Material mat)
            {
                if (!shaderTransferNewTargetMaterials.Contains(mat))
                {
                    shaderTransferNewTargetMaterials.Add(mat);
                    shaderTransferNewTargetSelected.Add(mat);
                }
            }
            else if (obj is GameObject go)
            {
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    foreach (var mat2 in renderer.sharedMaterials)
                    {
                        if (mat2 != null && !shaderTransferNewTargetMaterials.Contains(mat2))
                        {
                            shaderTransferNewTargetMaterials.Add(mat2);
                            shaderTransferNewTargetSelected.Add(mat2);
                        }
                    }
                }
            }
        }

        private void DrawShaderTransferStep3_Execute()
        {
            bool hasSource = shaderTransferNewSourceMaterial != null && LiltoonPresetReader.IsLiltoonMaterial(shaderTransferNewSourceMaterial);
            bool hasTargets = shaderTransferNewTargetSelected.Count > 0;
            bool canTransfer = hasSource && hasTargets && shaderTransferNewCategories != MaterialUnifyToolMethods.TransferCategories.None;

            GUI.enabled = hasSource;

            EditorGUILayout.LabelField("転送設定", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            // Category selection via EnumFlagsField
            shaderTransferNewCategories = (MaterialUnifyToolMethods.TransferCategories)EditorGUILayout.EnumFlagsField(
                "転送項目", shaderTransferNewCategories);

            if (shaderTransferNewCategories == MaterialUnifyToolMethods.TransferCategories.None)
            {
                EditorGUILayout.HelpBox("転送する項目を1つ以上選択してください。", MessageType.Info);
            }

            // Texture transfer options (foldout, shown only when relevant categories selected)
            bool hasTexCategories =
                (shaderTransferNewCategories & MaterialUnifyToolMethods.TransferCategories.反射) != 0 ||
                (shaderTransferNewCategories & MaterialUnifyToolMethods.TransferCategories.マットキャップ) != 0 ||
                (shaderTransferNewCategories & MaterialUnifyToolMethods.TransferCategories.マットキャップ2nd) != 0;

            if (hasTexCategories)
            {
                showShaderTransferTexOptions = EditorGUILayout.Foldout(showShaderTransferTexOptions, "テクスチャ転送オプション", true);
                if (showShaderTransferTexOptions)
                {
                    EditorGUI.indentLevel++;

                    if ((shaderTransferNewCategories & MaterialUnifyToolMethods.TransferCategories.反射) != 0)
                    {
                        transferReflectionCubeTex = EditorGUILayout.Toggle("反射キューブマップを転送", transferReflectionCubeTex);
                    }

                    if ((shaderTransferNewCategories & MaterialUnifyToolMethods.TransferCategories.マットキャップ) != 0)
                    {
                        transferMatCapTex = EditorGUILayout.Toggle("マットキャップ1を転送", transferMatCapTex);
                        transferMatCapBumpMask = EditorGUILayout.Toggle("マットキャップ1ノーマルマップも転送", transferMatCapBumpMask);
                    }

                    if ((shaderTransferNewCategories & MaterialUnifyToolMethods.TransferCategories.マットキャップ2nd) != 0)
                    {
                        transferMatCap2ndTex = EditorGUILayout.Toggle("マットキャップ2を転送", transferMatCap2ndTex);
                        transferMatCap2ndBumpMask = EditorGUILayout.Toggle("マットキャップ2ノーマルマップも転送", transferMatCap2ndBumpMask);
                    }

                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndVertical();

            GUILayout.Space(5);

            // Transfer button
            GUI.enabled = canTransfer;
            var validTargets = shaderTransferNewTargetMaterials.Where(m => m != null && shaderTransferNewTargetSelected.Contains(m) && LiltoonPresetReader.IsLiltoonMaterial(m)).ToList();

            float buttonHeight = EditorGUIUtility.singleLineHeight * 2f;
            if (GUILayout.Button($"転送 ({validTargets.Count}件)", GUILayout.Height(buttonHeight)))
            {
                MaterialUnifyToolMethods.TransferSelectedCategories(
                    shaderTransferNewSourceMaterial,
                    validTargets,
                    shaderTransferNewCategories,
                    transferReflectionCubeTex,
                    transferMatCapTex,
                    transferMatCapBumpMask,
                    transferMatCap2ndTex,
                    transferMatCap2ndBumpMask
                );

                EditorUtility.DisplayDialog(
                    LocalizationManager.Get("success_title"),
                    $"{validTargets.Count}件のマテリアルに設定を転送しました。",
                    LocalizationManager.Get("ok")
                );
            }
            GUI.enabled = true;
        }

        private void DrawShaderSettingsTabClassic()
        {
            EditorGUILayout.LabelField("シェーダー設定転送 (Material Unify Tool)", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // Tab selection
            string[] tabNames = {"簡単転送", "詳細転送"};
            shaderTransferTab = GUILayout.Toolbar(shaderTransferTab, tabNames);
            GUILayout.Space(10);

            switch (shaderTransferTab)
            {
                case 0:
                    DrawSimpleTransferTab();
                    break;
                case 1:
                    DrawAdvancedTransferTab();
                    break;
            }
        }

        private void AutoConfigureHighPrecisionSettings()
        {
            if (highPrecisionConfig.referenceGameObject == null) return;

            // Use material index based on selected reference material
            if (selectedReferenceMaterial != null)
            {
                var meshRenderer = highPrecisionConfig.referenceGameObject.GetComponent<MeshRenderer>();
                var skinnedMeshRenderer = highPrecisionConfig.referenceGameObject.GetComponent<SkinnedMeshRenderer>();

                Material[] materials = null;
                if (meshRenderer != null)
                {
                    materials = meshRenderer.sharedMaterials;
                }
                else if (skinnedMeshRenderer != null)
                {
                    materials = skinnedMeshRenderer.sharedMaterials;
                }

                if (materials != null)
                {
                    // Find the index of the selected reference material
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == selectedReferenceMaterial)
                        {
                            highPrecisionConfig.materialIndex = i;
                            break;
                        }
                    }
                }
            }
            else
            {
                // Fallback to index 0 if no material is selected
                highPrecisionConfig.materialIndex = 0;
            }

            // Set default values following MeshDeleterWithTexture approach
            highPrecisionConfig.uvChannel = 0; // Standard UV channel
            highPrecisionConfig.showVisualMask = false; // Remove visual mask option
        }

        private void GenerateUVMaskPreview()
        {
            if (selectedReferenceMaterial == null || highPrecisionConfig.referenceGameObject == null)
            {
                ClearUVMaskPreview();
                return;
            }

            // Get main texture from selected reference material
            var mainTexture = selectedReferenceMaterial.mainTexture as Texture2D;
            if (mainTexture == null)
            {
                ClearUVMaskPreview();
                return;
            }

            try
            {
                // Auto-configure settings first
                AutoConfigureHighPrecisionSettings();

                // Analyze UV usage
                var uvUsage = MeshUVAnalyzer.AnalyzeGameObjectUVUsage(
                    highPrecisionConfig.referenceGameObject,
                    mainTexture,
                    highPrecisionConfig.materialIndex,
                    highPrecisionConfig.uvChannel
                );

                if (uvUsage != null)
                {
                    // Create masked texture showing used vs unused areas
                    var maskColor = new Color(0.1f, 0.1f, 0.1f, 0.8f); // Dark gray for unused areas
                    var maskedTexture = MeshUVAnalyzer.CreateMaskedTexture(mainTexture, uvUsage, maskColor, 0.7f);

                    if (maskedTexture != null)
                    {
                        // Clear previous preview
                        ClearUVMaskPreview();

                        // Set new preview
                        uvMaskPreviewTexture = maskedTexture;

                        Debug.Log($"[UV Mask Preview] Generated for {highPrecisionConfig.referenceGameObject.name}: {uvUsage.usagePercentage:F1}% usage");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[UV Mask Preview] Failed to generate preview: {e.Message}");
                ClearUVMaskPreview();
            }
        }

        private void ClearUVMaskPreview()
        {
            if (uvMaskPreviewTexture != null)
            {
                DestroyImmediate(uvMaskPreviewTexture, true);
                uvMaskPreviewTexture = null;
            }
        }

        // Simple Transfer Tab (original functionality)
        private void DrawSimpleTransferTab()
        {
            EditorGUILayout.LabelField(LocalizationManager.Get("material_transfer"), EditorStyles.boldLabel);
            GUILayout.Space(10);

            // Help text with icon
            EditorGUILayout.HelpBox(LocalizationManager.Get("drawing_effects_help"), MessageType.Info);
            GUILayout.Space(10);

            // Material Transfer Flow Section
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("📤 Material Transfer Flow", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // Source Material Section
            EditorGUILayout.BeginVertical("helpbox");
            var sourceColor = sourceLiltoonMaterial != null && LiltoonPresetReader.IsLiltoonMaterial(sourceLiltoonMaterial) ? Color.green : Color.gray;
            GUI.color = sourceColor;
            EditorGUILayout.LabelField("🎨 " + LocalizationManager.Get("source_material"), EditorStyles.boldLabel);
            GUI.color = Color.white;

            // 説明テキストを追加
            EditorGUILayout.LabelField("（参考にしたい方）", EditorStyles.miniLabel);

            sourceLiltoonMaterial = (Material)EditorGUILayout.ObjectField(
                "",
                sourceLiltoonMaterial,
                typeof(Material),
                false
            );

            if (sourceLiltoonMaterial != null && !LiltoonPresetReader.IsLiltoonMaterial(sourceLiltoonMaterial))
            {
                EditorGUILayout.HelpBox(LocalizationManager.Get("materials_must_be_liltoon"), MessageType.Warning);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Transfer Arrow and Intensity
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // Create arrow with transfer info
            var arrowStyle = new GUIStyle(GUI.skin.label);
            arrowStyle.fontSize = 20;
            arrowStyle.alignment = TextAnchor.MiddleCenter;

            if (sourceLiltoonMaterial != null && targetLiltoonMaterials.Count > 0)
            {
                GUI.color = Color.cyan;
                EditorGUILayout.LabelField("⬇", arrowStyle, GUILayout.Width(30));
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = Color.gray;
                EditorGUILayout.LabelField("⬇", arrowStyle, GUILayout.Width(30));
                GUI.color = Color.white;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Target Materials Section (Multi-selection)
            EditorGUILayout.BeginVertical("helpbox");
            var hasValidTargets = targetLiltoonMaterials.Count > 0 && targetLiltoonMaterials.All(m => m != null && LiltoonPresetReader.IsLiltoonMaterial(m));
            var targetColor = hasValidTargets ? Color.green : Color.gray;
            GUI.color = targetColor;
            EditorGUILayout.LabelField("🎯 " + LocalizationManager.Get("target_material") + $" ({targetLiltoonMaterials.Count})", EditorStyles.boldLabel);
            GUI.color = Color.white;

            // 説明テキストを追加
            EditorGUILayout.LabelField("（変更する方・複数選択可）", EditorStyles.miniLabel);

            // Draw existing target materials
            for (int i = 0; i < targetLiltoonMaterials.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                var material = targetLiltoonMaterials[i];
                var isValidMaterial = material != null && LiltoonPresetReader.IsLiltoonMaterial(material);

                if (!isValidMaterial)
                {
                    GUI.color = Color.red;
                }

                targetLiltoonMaterials[i] = (Material)EditorGUILayout.ObjectField(
                    "",
                    targetLiltoonMaterials[i],
                    typeof(Material),
                    false
                );

                GUI.color = Color.white;

                // Remove button
                if (GUILayout.Button("❌", GUILayout.Width(25)))
                {
                    targetLiltoonMaterials.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();

                if (material != null && !LiltoonPresetReader.IsLiltoonMaterial(material))
                {
                    EditorGUILayout.HelpBox(LocalizationManager.Get("materials_must_be_liltoon"), MessageType.Warning);
                }
            }

            GUILayout.Space(5);

            // Add new target material button
            if (GUILayout.Button("➕ 転送先マテリアルを追加", GUILayout.Height(25)))
            {
                targetLiltoonMaterials.Add(null);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // Status Preview
            if (sourceLiltoonMaterial != null && targetLiltoonMaterials.Count > 0)
            {
                EditorGUILayout.BeginVertical("box");
                GUI.color = Color.cyan;
                EditorGUILayout.LabelField("✅ Ready for Transfer", EditorStyles.boldLabel);
                GUI.color = Color.white;

                EditorGUILayout.LabelField($"📤 From: {sourceLiltoonMaterial.name}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"📥 To: {targetLiltoonMaterials.Count} material(s)", EditorStyles.miniLabel);

                // Show target material names
                for (int i = 0; i < targetLiltoonMaterials.Count && i < 5; i++)
                {
                    if (targetLiltoonMaterials[i] != null)
                    {
                        EditorGUILayout.LabelField($"  • {targetLiltoonMaterials[i].name}", EditorStyles.miniLabel);
                    }
                }

                if (targetLiltoonMaterials.Count > 5)
                {
                    EditorGUILayout.LabelField($"  ... and {targetLiltoonMaterials.Count - 5} more", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(10);
            }

            // Transfer Button
            var validTargets = targetLiltoonMaterials.Where(m => m != null && LiltoonPresetReader.IsLiltoonMaterial(m)).ToList();
            bool canTransfer = sourceLiltoonMaterial != null &&
                             LiltoonPresetReader.IsLiltoonMaterial(sourceLiltoonMaterial) &&
                             validTargets.Count > 0;

            EditorGUI.BeginDisabledGroup(!canTransfer);

            // Styled transfer button
            var buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 14;
            buttonStyle.fontStyle = FontStyle.Bold;

            if (canTransfer)
            {
                GUI.color = Color.green;
            }
            else
            {
                GUI.color = Color.gray;
            }

            if (GUILayout.Button($"🚀 {LocalizationManager.Get("drawing_effects_only")} ({validTargets.Count}件)", buttonStyle, GUILayout.Height(35)))
            {
                // Perform the transfer to all valid targets
                foreach (var targetMaterial in validTargets)
                {
                    LiltoonPresetApplier.TransferDrawingEffects(
                        sourceLiltoonMaterial,
                        targetMaterial,
                        1.0f
                    );
                }

                EditorUtility.DisplayDialog(
                    LocalizationManager.Get("success_title"),
                    $"Drawing effects transferred to {validTargets.Count} materials successfully!",
                    LocalizationManager.Get("ok")
                );
            }

            GUI.color = Color.white;
            EditorGUI.EndDisabledGroup();

            if (!canTransfer)
            {
                if (sourceLiltoonMaterial == null)
                {
                    EditorGUILayout.HelpBox("⚠️ 転送元マテリアルを選択してください", MessageType.Warning);
                }
                else if (validTargets.Count == 0)
                {
                    EditorGUILayout.HelpBox("⚠️ 有効な転送先マテリアルを選択してください", MessageType.Warning);
                }
                else if (!LiltoonPresetReader.IsLiltoonMaterial(sourceLiltoonMaterial))
                {
                    EditorGUILayout.HelpBox("❌ " + LocalizationManager.Get("materials_must_be_liltoon"), MessageType.Error);
                }
            }
        }

        // Advanced Transfer Tab (Material Unify Tool functionality)
        private void DrawAdvancedTransferTab()
        {
            EditorGUILayout.LabelField("選択的転送 (Material Unify Tool)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("liltoon マテリアル設定の選択的転送ツール", EditorStyles.miniBoldLabel);
            EditorGUILayout.Space();

            DrawMaterialUnifyGameObjectSelection();
            EditorGUILayout.Space();

            DrawMaterialUnifyMaterialSelection();
            EditorGUILayout.Space();

            DrawMaterialUnifyCategorySelection();
            EditorGUILayout.Space();

            DrawMaterialUnifyTextureTransferOptions();
            EditorGUILayout.Space();

            DrawMaterialUnifyTransferButton();
        }

        // MaterialUnifyTool GameObject Selection
        private void DrawMaterialUnifyGameObjectSelection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("GameObject選択", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                ClearMaterialUnifyAllSelections();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Hierarchyからアバター，小物を追加してください", EditorStyles.helpBox);

            // Source GameObject selection
            var newSourceGameObject = (GameObject)EditorGUILayout.ObjectField(
                "転送元GameObject", shaderTransferSourceGameObject, typeof(GameObject), true);

            if (newSourceGameObject != shaderTransferSourceGameObject)
            {
                shaderTransferSourceGameObject = newSourceGameObject;
                UpdateShaderTransferSourceMaterials();
            }

            // Target GameObject selection with list support
            EditorGUILayout.LabelField("転送先GameObject（複数可）", EditorStyles.miniBoldLabel);

            // Add new target button
            if (GUILayout.Button("転送先GameObjectを追加"))
            {
                shaderTransferTargetGameObjects.Add(null);
            }

            // Display target GameObjects list
            for (int i = shaderTransferTargetGameObjects.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();

                var newTargetGameObject = (GameObject)EditorGUILayout.ObjectField(
                    $"転送先 {i + 1}", shaderTransferTargetGameObjects[i], typeof(GameObject), true);

                if (newTargetGameObject != shaderTransferTargetGameObjects[i])
                {
                    shaderTransferTargetGameObjects[i] = newTargetGameObject;
                    UpdateShaderTransferTargetMaterials();
                }

                if (GUILayout.Button("削除", GUILayout.Width(50)))
                {
                    shaderTransferTargetGameObjects.RemoveAt(i);
                    UpdateShaderTransferTargetMaterials();
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        // MaterialUnifyTool Material Selection
        private void DrawMaterialUnifyMaterialSelection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("マテリアル選択", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            var newIncludeInactiveObjects = EditorGUILayout.Toggle("見えていないマテリアルも使用", includeInactiveObjects);
            if (newIncludeInactiveObjects != includeInactiveObjects)
            {
                includeInactiveObjects = newIncludeInactiveObjects;
                UpdateShaderTransferSourceMaterials();
                UpdateShaderTransferTargetMaterials();
            }

            EditorGUILayout.EndHorizontal();

            // Source materials
            if (shaderTransferSourceAvailableMaterials.Count > 0)
            {
                EditorGUILayout.LabelField("転送元マテリアル（単一選択）", EditorStyles.miniBoldLabel);

                for (int i = 0; i < shaderTransferSourceAvailableMaterials.Count; i++)
                {
                    var material = shaderTransferSourceAvailableMaterials[i];
                    var isLiltoon = MaterialUnifyToolMethods.IsLiltoonMaterial(material);

                    EditorGUILayout.BeginHorizontal();

                    // Change color based on liltoon compatibility
                    var originalColor = GUI.color;
                    GUI.color = isLiltoon ? Color.white : Color.yellow;

                    bool isSelected = shaderTransferSelectedSourceMaterialIndex == i;
                    bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));

                    if (newSelected != isSelected)
                    {
                        shaderTransferSelectedSourceMaterialIndex = newSelected ? i : -1;
                    }

                    // Foldout for material details
                    if (!shaderTransferSourceMaterialFoldouts.ContainsKey(material))
                        shaderTransferSourceMaterialFoldouts[material] = false;

                    var foldoutRect = GUILayoutUtility.GetRect(15, EditorGUIUtility.singleLineHeight);
                    shaderTransferSourceMaterialFoldouts[material] = EditorGUI.Foldout(
                        foldoutRect,
                        shaderTransferSourceMaterialFoldouts[material],
                        "");

                    if (GUILayout.Button($"{material.name} {(isLiltoon ? "" : "(非liltoon)")}", EditorStyles.label))
                    {
                        Selection.activeObject = material;
                        EditorGUIUtility.PingObject(material);
                    }

                    GUI.color = originalColor;

                    EditorGUILayout.EndHorizontal();

                    // Show GameObjects using this material if foldout is open
                    if (shaderTransferSourceMaterialFoldouts[material] && shaderTransferSourceMaterialToGameObjects.ContainsKey(material))
                    {
                        EditorGUI.indentLevel++;
                        foreach (var gameObject in shaderTransferSourceMaterialToGameObjects[material])
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(20);
                            EditorGUILayout.LabelField($"• {gameObject.name}", EditorStyles.miniLabel);

                            if (GUILayout.Button("Select", GUILayout.Width(60)))
                            {
                                Selection.activeGameObject = gameObject;
                                EditorGUIUtility.PingObject(gameObject);
                            }

                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }
            else if (shaderTransferSourceGameObject != null)
            {
                EditorGUILayout.HelpBox("転送元GameObjectにRendererが見つかりません。", MessageType.Warning);
            }

            EditorGUILayout.Space();

            // Target materials
            if (shaderTransferTargetAvailableMaterials.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("転送先マテリアル（複数選択可）", EditorStyles.miniBoldLabel);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("All", GUILayout.Width(40)))
                {
                    for (int i = 0; i < shaderTransferTargetSelectedMaterials.Count; i++)
                    {
                        shaderTransferTargetSelectedMaterials[i] = true;
                    }
                }

                if (GUILayout.Button("None", GUILayout.Width(50)))
                {
                    for (int i = 0; i < shaderTransferTargetSelectedMaterials.Count; i++)
                    {
                        shaderTransferTargetSelectedMaterials[i] = false;
                    }
                }

                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < shaderTransferTargetAvailableMaterials.Count; i++)
                {
                    var material = shaderTransferTargetAvailableMaterials[i];
                    var isLiltoon = MaterialUnifyToolMethods.IsLiltoonMaterial(material);

                    EditorGUILayout.BeginHorizontal();

                    // Change color based on liltoon compatibility
                    var originalColor = GUI.color;
                    GUI.color = isLiltoon ? Color.white : Color.yellow;

                    shaderTransferTargetSelectedMaterials[i] = EditorGUILayout.Toggle(
                        shaderTransferTargetSelectedMaterials[i], GUILayout.Width(20));

                    // Foldout for material details
                    if (!shaderTransferTargetMaterialFoldouts.ContainsKey(material))
                        shaderTransferTargetMaterialFoldouts[material] = false;

                    var foldoutRect = GUILayoutUtility.GetRect(15, EditorGUIUtility.singleLineHeight);
                    shaderTransferTargetMaterialFoldouts[material] = EditorGUI.Foldout(
                        foldoutRect,
                        shaderTransferTargetMaterialFoldouts[material],
                        "");

                    if (GUILayout.Button($"{material.name} {(isLiltoon ? "" : "(非liltoon)")}", EditorStyles.label))
                    {
                        Selection.activeObject = material;
                        EditorGUIUtility.PingObject(material);
                    }

                    GUI.color = originalColor;

                    EditorGUILayout.EndHorizontal();

                    // Show GameObjects using this material if foldout is open
                    if (shaderTransferTargetMaterialFoldouts[material] && shaderTransferTargetMaterialToGameObjects.ContainsKey(material))
                    {
                        EditorGUI.indentLevel++;
                        foreach (var gameObject in shaderTransferTargetMaterialToGameObjects[material])
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(20);
                            EditorGUILayout.LabelField($"• {gameObject.name}", EditorStyles.miniLabel);

                            if (GUILayout.Button("Select", GUILayout.Width(60)))
                            {
                                Selection.activeGameObject = gameObject;
                                EditorGUIUtility.PingObject(gameObject);
                            }

                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }
            else if (shaderTransferTargetGameObjects.Count > 0 && shaderTransferTargetGameObjects.Any(go => go != null))
            {
                EditorGUILayout.HelpBox("転送先GameObjectにRendererが見つかりません。", MessageType.Warning);
            }
        }

        // MaterialUnifyTool Category Selection
        private void DrawMaterialUnifyCategorySelection()
        {
            EditorGUILayout.LabelField("転送する項目を選択", EditorStyles.boldLabel);

            selectedCategories = (MaterialUnifyToolMethods.TransferCategories)EditorGUILayout.EnumFlagsField(
                "転送項目", selectedCategories);

            if (selectedCategories == MaterialUnifyToolMethods.TransferCategories.None)
            {
                EditorGUILayout.HelpBox("転送する項目を1つ以上選択してください。", MessageType.Info);
            }
        }

        // MaterialUnifyTool Texture Transfer Options
        private void DrawMaterialUnifyTextureTransferOptions()
        {
            EditorGUILayout.LabelField("テクスチャ転送オプション", EditorStyles.boldLabel);

            // Show texture options only if relevant categories are selected
            if ((selectedCategories & MaterialUnifyToolMethods.TransferCategories.反射) != 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                transferReflectionCubeTex = EditorGUILayout.Toggle(transferReflectionCubeTex, GUILayout.Width(20));
                EditorGUILayout.LabelField("反射 > キューブマップを転送");
                EditorGUILayout.EndHorizontal();
            }

            if ((selectedCategories & MaterialUnifyToolMethods.TransferCategories.マットキャップ) != 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                transferMatCapTex = EditorGUILayout.Toggle(transferMatCapTex, GUILayout.Width(20));
                EditorGUILayout.LabelField("マットキャップ 1を転送");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                transferMatCapBumpMask = EditorGUILayout.Toggle(transferMatCapBumpMask, GUILayout.Width(20));
                EditorGUILayout.LabelField("マットキャップ 1のカスタムノーマルマップも転送");
                EditorGUILayout.EndHorizontal();
            }

            if ((selectedCategories & MaterialUnifyToolMethods.TransferCategories.マットキャップ2nd) != 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                transferMatCap2ndTex = EditorGUILayout.Toggle(transferMatCap2ndTex, GUILayout.Width(20));
                EditorGUILayout.LabelField("マットキャップ 2を転送");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                transferMatCap2ndBumpMask = EditorGUILayout.Toggle(transferMatCap2ndBumpMask, GUILayout.Width(20));
                EditorGUILayout.LabelField("マットキャップ 2のカスタムノーマルマップも転送");
                EditorGUILayout.EndHorizontal();
            }
        }

        // MaterialUnifyTool Transfer Button
        private void DrawMaterialUnifyTransferButton()
        {
            var hasSelectedSourceMaterial = shaderTransferSelectedSourceMaterialIndex >= 0;
            var hasSelectedTargetMaterials = shaderTransferTargetSelectedMaterials.Any(selected => selected);

            EditorGUI.BeginDisabledGroup(
                !hasSelectedSourceMaterial ||
                !hasSelectedTargetMaterials ||
                selectedCategories == MaterialUnifyToolMethods.TransferCategories.None);

            if (GUILayout.Button("選択した項目を転送", GUILayout.Height(30)))
            {
                TransferMaterialUnifySelectedCategories();
            }

            EditorGUI.EndDisabledGroup();

            // Show status info
            if (!hasSelectedSourceMaterial && shaderTransferSourceAvailableMaterials.Count > 0)
            {
                EditorGUILayout.HelpBox("転送元マテリアルを1つ選択してください。", MessageType.Info);
            }

            if (!hasSelectedTargetMaterials && shaderTransferTargetAvailableMaterials.Count > 0)
            {
                EditorGUILayout.HelpBox("転送先マテリアルを1つ以上選択してください。", MessageType.Info);
            }
        }

        // MaterialUnifyTool Helper Methods
        private void UpdateShaderTransferSourceMaterials()
        {
            shaderTransferSourceAvailableMaterials.Clear();
            shaderTransferSourceMaterialToGameObjects.Clear();
            shaderTransferSourceMaterialFoldouts.Clear();
            shaderTransferSelectedSourceMaterialIndex = -1;

            if (shaderTransferSourceGameObject != null)
            {
                var renderers = shaderTransferSourceGameObject.GetComponentsInChildren<Renderer>(includeInactiveObjects);

                foreach (var renderer in renderers)
                {
                    // Skip if renderer is on inactive GameObject and includeInactiveObjects is false
                    if (!includeInactiveObjects && !renderer.gameObject.activeInHierarchy)
                        continue;

                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != null)
                        {
                            if (!shaderTransferSourceAvailableMaterials.Contains(material))
                            {
                                shaderTransferSourceAvailableMaterials.Add(material);
                                shaderTransferSourceMaterialToGameObjects[material] = new List<GameObject>();
                            }

                            if (!shaderTransferSourceMaterialToGameObjects[material].Contains(renderer.gameObject))
                            {
                                shaderTransferSourceMaterialToGameObjects[material].Add(renderer.gameObject);
                            }
                        }
                    }
                }
            }
        }

        private void UpdateShaderTransferTargetMaterials()
        {
            shaderTransferTargetAvailableMaterials.Clear();
            shaderTransferTargetSelectedMaterials.Clear();
            shaderTransferTargetMaterialToGameObjects.Clear();
            shaderTransferTargetMaterialFoldouts.Clear();

            foreach (var targetGameObject in shaderTransferTargetGameObjects)
            {
                if (targetGameObject != null)
                {
                    var renderers = targetGameObject.GetComponentsInChildren<Renderer>(includeInactiveObjects);

                    foreach (var renderer in renderers)
                    {
                        // Skip if renderer is on inactive GameObject and includeInactiveObjects is false
                        if (!includeInactiveObjects && !renderer.gameObject.activeInHierarchy)
                            continue;

                        foreach (var material in renderer.sharedMaterials)
                        {
                            if (material != null)
                            {
                                if (!shaderTransferTargetAvailableMaterials.Contains(material))
                                {
                                    shaderTransferTargetAvailableMaterials.Add(material);
                                    shaderTransferTargetSelectedMaterials.Add(false);
                                    shaderTransferTargetMaterialToGameObjects[material] = new List<GameObject>();
                                }

                                if (!shaderTransferTargetMaterialToGameObjects[material].Contains(renderer.gameObject))
                                {
                                    shaderTransferTargetMaterialToGameObjects[material].Add(renderer.gameObject);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ClearMaterialUnifyAllSelections()
        {
            // Clear GameObjects
            shaderTransferSourceGameObject = null;
            shaderTransferTargetGameObjects.Clear();

            // Clear materials
            shaderTransferSourceAvailableMaterials.Clear();
            shaderTransferSelectedSourceMaterialIndex = -1;
            shaderTransferTargetAvailableMaterials.Clear();
            shaderTransferTargetSelectedMaterials.Clear();
            shaderTransferSourceMaterialToGameObjects.Clear();
            shaderTransferTargetMaterialToGameObjects.Clear();
            shaderTransferSourceMaterialFoldouts.Clear();
            shaderTransferTargetMaterialFoldouts.Clear();

            // Clear transfer settings
            selectedCategories = MaterialUnifyToolMethods.TransferCategories.None;
            transferReflectionCubeTex = false;
            transferMatCapTex = false;
            transferMatCapBumpMask = false;
            transferMatCap2ndTex = false;
            transferMatCap2ndBumpMask = false;

            // Clear options
            includeInactiveObjects = false;
        }

        private void TransferMaterialUnifySelectedCategories()
        {
            if (shaderTransferSelectedSourceMaterialIndex < 0 || shaderTransferSelectedSourceMaterialIndex >= shaderTransferSourceAvailableMaterials.Count)
            {
                EditorUtility.DisplayDialog("エラー", "転送元マテリアルを選択してください。", "OK");
                return;
            }

            var selectedTargetMaterials = GetShaderTransferSelectedMaterials(shaderTransferTargetAvailableMaterials, shaderTransferTargetSelectedMaterials);

            if (selectedTargetMaterials.Count == 0)
            {
                EditorUtility.DisplayDialog("エラー", "転送先マテリアルを選択してください。", "OK");
                return;
            }

            var sourceMaterial = shaderTransferSourceAvailableMaterials[shaderTransferSelectedSourceMaterialIndex];

            MaterialUnifyToolMethods.TransferSelectedCategories(
                sourceMaterial, selectedTargetMaterials, selectedCategories,
                transferReflectionCubeTex, transferMatCapTex, transferMatCapBumpMask,
                transferMatCap2ndTex, transferMatCap2ndBumpMask);
        }

        private List<Material> GetShaderTransferSelectedMaterials(List<Material> availableMaterials, List<bool> selectedMaterials)
        {
            var result = new List<Material>();

            for (int i = 0; i < availableMaterials.Count && i < selectedMaterials.Count; i++)
            {
                if (selectedMaterials[i])
                {
                    result.Add(availableMaterials[i]);
                }
            }

            return result;
        }
    }
}
