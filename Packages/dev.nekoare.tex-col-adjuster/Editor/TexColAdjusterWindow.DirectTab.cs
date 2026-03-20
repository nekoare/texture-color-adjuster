using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using TexColAdjuster.Editor;
using TexColAdjuster.Editor.Models;
using ColorAdjustmentMode = TexColAdjuster.Runtime.ColorAdjustmentMode;

namespace TexColAdjuster
{
    public partial class TexColAdjusterWindow
    {
        private void HandleDirectTabAutoPreview()
        {
            if (!CanProcessExperimental())
            {
                CancelDirectTabAutoPreview();
                return;
            }

            if (useHighPrecisionMode && !IsHighPrecisionConfigValidForDirectTab())
            {
                CancelDirectTabAutoPreview();
                return;
            }

            // スポイトモードで2点揃っていない場合はプレビュー更新をスキップ
            if (useDualColorSelection && !(hasSelectedTargetColor && hasSelectedReferenceColor))
            {
                return;
            }

            var currentState = CaptureCurrentDirectPreviewState();

            if (HasParametersChanged())
            {
                // Post-adjustment only change with cached LAB result → immediate re-apply
                if (HasOnlyPostAdjustmentChanged() && _cachedLabMatchRT != null)
                {
                    ReapplyPostAdjustmentsFromCache();
                    return;
                }

                RequestDirectTabAutoPreview(currentState);
            }
            else if (directTabHasQueuedParameters && !directTabQueuedParameterState.Equals(currentState))
            {
                RequestDirectTabAutoPreview(currentState);
            }

            TryExecuteDirectTabAutoPreview();
        }

        private void RequestDirectTabAutoPreview(DirectPreviewParameterState state)
        {
            double now = EditorApplication.timeSinceStartup;

            if (!directTabPreviewPending || !directTabHasQueuedParameters || !directTabQueuedParameterState.Equals(state))
            {
                bool canUseGPU = GPUColorAdjuster.IsGPUProcessingAvailable()
                    && adjustmentMode == TexColAdjuster.Runtime.ColorAdjustmentMode.LabHistogramMatching
                    && !useDualColorSelection;
                double debounce = canUseGPU ? DirectTabPreviewDebounceSecondsGPU : DirectTabPreviewDebounceSecondsCPU;
                directTabNextPreviewTime = now + debounce;
                directTabQueuedParameterState = state;
                directTabHasQueuedParameters = true;
            }

            directTabPreviewPending = true;
            Repaint();
        }

        private void TryExecuteDirectTabAutoPreview()
        {
            if (directTabPreviewInFlight || !directTabPreviewPending)
                return;

            if (EditorApplication.timeSinceStartup < directTabNextPreviewTime)
                return;

            if (!CanProcessExperimental())
            {
                CancelDirectTabAutoPreview();
                return;
            }

            directTabPreviewPending = false;
            directTabPreviewInFlight = true;

            try
            {
                if (useHighPrecisionMode && IsHighPrecisionConfigValidForDirectTab())
                {
                    GenerateHighPrecisionPreviewForDirectTab();
                }
                else
                {
                    GenerateExperimentalPreview();
                }
            }
            finally
            {
                directTabPreviewInFlight = false;
                directTabHasQueuedParameters = false;
            }
        }

        private void CancelDirectTabAutoPreview(bool resetQueuedState = true)
        {
            directTabPreviewPending = false;
            directTabPreviewInFlight = false;
            directTabNextPreviewTime = 0d;

            if (resetQueuedState)
            {
                directTabHasQueuedParameters = false;
                directTabQueuedParameterState = default;
            }
        }

        private DirectPreviewParameterState CaptureCurrentDirectPreviewState()
        {
            return new DirectPreviewParameterState(
                adjustmentIntensity,
                preserveLuminance,
                preserveTexture,
                adjustmentMode,
                useDualColorSelection,
                selectedTargetColor,
                selectedReferenceColor,
                hasSelectedTargetColor,
                hasSelectedReferenceColor,
                colorSelectionRange,
                useHighPrecisionMode,
                hueShift,
                saturationMultiplier,
                brightnessOffset,
                contrastMultiplier,
                gammaCorrection,
                midtoneShift
            );
        }

        private void HandleDirectMaterialSelectionChanged()
        {
            ClearPreview();
            RestoreUncompressedTextureCache();

            if (CanProcessExperimental())
            {
                RequestDirectTabAutoPreview(CaptureCurrentDirectPreviewState());
            }
        }

        private bool IsHighPrecisionConfigValidForDirectTab()
        {
            if (highPrecisionConfig == null)
            {
                return false;
            }

            var referenceTexture = GetExperimentalReferenceTexture();
            if (referenceTexture == null)
            {
                return false;
            }

            return HighPrecisionProcessor.ValidateHighPrecisionConfig(highPrecisionConfig, referenceTexture);
        }

        private void DrawDirectTab()
        {
            if (useNewUI)
                DrawDirectTabNew();
            else
                DrawDirectTabClassic();
        }

        private void DrawDirectTabNew()
        {
            // Step 1: Target selection
            DrawPartSelectionBox(LocalizationManager.Get("part_target"),
                ref targetGameObject, ref targetComponent, ref selectedTargetMaterial, true);

            GUILayout.Space(8);

            DrawPartSelectionBox(LocalizationManager.Get("part_reference"),
                ref referenceGameObject, ref referenceComponent, ref selectedReferenceMaterial, false);

            GUILayout.Space(10);

            // Step 2: Adjustment
            DrawDirectTabStep2_Adjustment();

            GUILayout.Space(10);

            // Step 3: Apply
            DrawDirectTabStep3_Apply();
        }

        private void DrawPartSelectionBox(string label, ref GameObject gameObject, ref Component component, ref Material selectedMaterial, bool isTarget)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            var newGO = EditorGUILayout.ObjectField(gameObject, typeof(GameObject), true) as GameObject;
            if (newGO != gameObject)
            {
                if (isTarget)
                {
                    RestoreScenePreview();
                }
                RestoreUncompressedTextureCache();
                gameObject = newGO;
                component = GetRendererComponent(gameObject);
                selectedMaterial = null;

                // Reference side: update highPrecisionConfig
                if (!isTarget && highPrecisionConfig != null)
                {
                    highPrecisionConfig.referenceGameObject = newGO;
                    ClearPreview();
                    uvUsageStats = "";

                    if (useHighPrecisionMode)
                    {
                        GenerateUVMaskPreview();
                    }
                }

                if (previewTexture != null)
                {
                    TextureColorSpaceUtility.UnregisterRuntimeTexture(previewTexture);
                    UnityEngine.Object.DestroyImmediate(previewTexture, true);
                    previewTexture = null;
                }
                CheckForExperimentalAutoPreview();
                UpdateCurrentMeshInfo();
            }

            if (gameObject != null && component == null)
            {
                EditorGUILayout.HelpBox(LocalizationManager.Get("renderer_not_found_short"), MessageType.Warning);
            }
            else if (component != null)
            {
                var materials = ExtractMaterials(component);
                DrawMaterialSelectionThumbnails(materials, ref selectedMaterial, () => {
                    HandleDirectMaterialSelectionChanged();
                    CheckForExperimentalAutoPreview();
                    if (!isTarget && useHighPrecisionMode)
                    {
                        GenerateUVMaskPreview();
                    }
                });
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMaterialSelectionThumbnails(Material[] materials, ref Material selectedMaterial, System.Action onChanged)
        {
            if (materials == null || materials.Length == 0) return;

            // Single material: auto-select
            if (materials.Length == 1)
            {
                if (selectedMaterial != materials[0])
                {
                    selectedMaterial = materials[0];
                    onChanged?.Invoke();
                }
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(selectedMaterial.name, EditorStyles.boldLabel);
                if (IsLiltoonMaterial(selectedMaterial))
                {
                    GUI.color = Color.green;
                    EditorGUILayout.LabelField("✓ liltoon", GUILayout.Width(60));
                }
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
                return;
            }

            // Multiple materials: thumbnail buttons
            float btnSize = 48f;
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null) continue;

                bool isSelected = mat == selectedMaterial;

                // Highlight selected
                var prevBgColor = GUI.backgroundColor;
                if (isSelected)
                    GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);

                var preview = AssetPreview.GetAssetPreview(mat);
                var content = preview != null
                    ? new GUIContent(preview, mat.name)
                    : new GUIContent(mat.name);

                if (GUILayout.Button(content, GUILayout.Width(btnSize), GUILayout.Height(btnSize)))
                {
                    if (!isSelected)
                    {
                        selectedMaterial = mat;
                        onChanged?.Invoke();
                    }
                }

                GUI.backgroundColor = prevBgColor;
            }
            EditorGUILayout.EndHorizontal();

            // Show selected material name
            if (selectedMaterial != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(selectedMaterial.name, EditorStyles.boldLabel);
                if (IsLiltoonMaterial(selectedMaterial))
                {
                    GUI.color = Color.green;
                    EditorGUILayout.LabelField("✓ liltoon", GUILayout.Width(60));
                }
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            // Auto-select first if none selected
            if (selectedMaterial == null && materials.Length > 0)
            {
                selectedMaterial = materials[0];
                onChanged?.Invoke();
            }
        }

        private void DrawDirectTabStep2_Adjustment()
        {
            bool canAdjust = CanProcessExperimental();

            EditorGUILayout.LabelField(LocalizationManager.Get("adjustment_section"), EditorStyles.boldLabel);

            GUI.enabled = canAdjust;
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("明るさ:", GUILayout.Width(150));
            brightnessOffset = EditorGUILayout.Slider(brightnessOffset, -1f, 1f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("鮮やかさ:", GUILayout.Width(150));
            saturationMultiplier = EditorGUILayout.Slider(saturationMultiplier, 0f, 2f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ガンマ:", GUILayout.Width(150));
            gammaCorrection = EditorGUILayout.Slider(gammaCorrection, 0.2f, 5f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("色合いシフト:", GUILayout.Width(150));
            midtoneShift = EditorGUILayout.Slider(midtoneShift, -0.5f, 0.5f);
            EditorGUILayout.EndHorizontal();

            // Reset button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(LocalizationManager.Get("reset_adjustments"), GUILayout.Width(80)))
            {
                adjustmentIntensity = 100f;
                brightnessOffset = 0f;
                gammaCorrection = 1f;
                saturationMultiplier = 1f;
                midtoneShift = 0f;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // Material transfer section
            if (selectedReferenceMaterial != null && selectedTargetMaterial != null)
            {
                GUILayout.Space(5);
                bool prevTransfer = enableMaterialTransfer;
                enableMaterialTransfer = EditorGUILayout.Toggle(LocalizationManager.Get("material_transfer_toggle"), enableMaterialTransfer);
                if (prevTransfer != enableMaterialTransfer)
                    RefreshScenePreviewMaterial();

                if (enableMaterialTransfer)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.HelpBox(LocalizationManager.Get("material_transfer_help"), MessageType.Info);

                    GUILayout.Space(5);
                    DrawMaterialTransferDirection();

                    EditorGUILayout.EndVertical();
                }
            }

            GUI.enabled = true;
        }

        private void DrawDirectTabStep3_Apply()
        {
            // Advanced settings (foldout, default closed)
            showAdvancedSettingsFoldout = EditorGUILayout.Foldout(showAdvancedSettingsFoldout, LocalizationManager.Get("advanced_settings"), true);
            if (showAdvancedSettingsFoldout)
            {
                EditorGUILayout.BeginVertical("box");

                adjustmentIntensity = EditorGUILayout.Slider(LocalizationManager.Get("adjustment_intensity"), adjustmentIntensity, 0f, 100f);
                preserveLuminance = EditorGUILayout.Toggle(LocalizationManager.Get("preserve_luminance"), preserveLuminance);

                string[] modeNames = LocalizationManager.GetColorAdjustmentModeDisplayNames();
                int modeIndex = (int)adjustmentMode;
                modeIndex = EditorGUILayout.Popup(LocalizationManager.Get("adjustment_mode"), modeIndex, modeNames);
                adjustmentMode = (ColorAdjustmentMode)modeIndex;

                // Dual color selection mode
                if (GetExperimentalReferenceTexture() != null && GetExperimentalTargetTexture() != null)
                {
                    GUILayout.Space(5);
                    useDualColorSelection = EditorGUILayout.Toggle(LocalizationManager.Get("enable_color_selection"), useDualColorSelection);
                    if (useDualColorSelection)
                    {
                        DrawDualColorSelectionUIForExperimental();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            // NDMF settings (foldout, default closed)
            showNDMFSettingsFoldout = EditorGUILayout.Foldout(showNDMFSettingsFoldout, LocalizationManager.Get("ndmf_settings"), true);
            if (showNDMFSettingsFoldout)
            {
                DrawNDMFOptionsForDirectTab();
            }

            GUILayout.Space(5);

            // Preview options
            bool newShowWindowPreview = EditorGUILayout.Toggle(LocalizationManager.Get("window_preview_toggle"), showWindowPreview);
            if (newShowWindowPreview != showWindowPreview)
            {
                showWindowPreview = newShowWindowPreview;
                EditorPrefs.SetBool("TexColAdjuster_ShowWindowPreview", showWindowPreview);

                // トグルON時にプレビューがなければ生成
                if (showWindowPreview && previewTexture == null && CanProcessExperimental())
                {
                    GenerateExperimentalPreview();
                }
            }

            if (showWindowPreview && previewTexture != null)
            {
                var refTex = GetExperimentalReferenceTexture();
                if (refTex != null)
                {
                    DrawReferenceAndAdjustedPreview(refTex, previewTexture);
                }
            }

            GUILayout.Space(5);

            // Action buttons (reuse existing)
            DrawDirectTabActionButtons();
        }

        private void DrawMaterialTransferDirection()
        {
            EditorGUILayout.LabelField(LocalizationManager.Get("transfer_direction"), EditorStyles.boldLabel);

            // Direction 0: Reference → Target
            EditorGUILayout.BeginHorizontal();
            bool direction0Selected = materialTransferDirection == 0;
            if (direction0Selected) GUI.color = Color.green;
            bool newDirection0 = EditorGUILayout.Toggle(direction0Selected, GUILayout.Width(20));
            if (newDirection0 && !direction0Selected)
            {
                materialTransferDirection = 0;
                RefreshScenePreviewMaterial();
            }
            GUI.color = Color.white;
            EditorGUILayout.LabelField($"参照用 ({(selectedReferenceMaterial != null ? selectedReferenceMaterial.name : LocalizationManager.Get("not_selected"))}) ", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("→", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(20));
            EditorGUILayout.LabelField($" 変更対象 ({(selectedTargetMaterial != null ? selectedTargetMaterial.name : LocalizationManager.Get("not_selected"))})", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // Direction 1: Target → Reference
            EditorGUILayout.BeginHorizontal();
            bool direction1Selected = materialTransferDirection == 1;
            if (direction1Selected) GUI.color = Color.green;
            bool newDirection1 = EditorGUILayout.Toggle(direction1Selected, GUILayout.Width(20));
            if (newDirection1 && !direction1Selected)
            {
                materialTransferDirection = 1;
                RefreshScenePreviewMaterial();
            }
            GUI.color = Color.white;
            EditorGUILayout.LabelField($"変更対象 ({(selectedTargetMaterial != null ? selectedTargetMaterial.name : LocalizationManager.Get("not_selected"))}) ", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("→", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(20));
            EditorGUILayout.LabelField($" 参照用 ({(selectedReferenceMaterial != null ? selectedReferenceMaterial.name : LocalizationManager.Get("not_selected"))})", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // Liltoon compatibility
            Material sourceMaterial = materialTransferDirection == 0 ? selectedReferenceMaterial : selectedTargetMaterial;
            Material targetMaterial = materialTransferDirection == 0 ? selectedTargetMaterial : selectedReferenceMaterial;
            bool sourceLiltoon = IsLiltoonMaterial(sourceMaterial);
            bool targetLiltoon = IsLiltoonMaterial(targetMaterial);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationManager.Get("transfer_source"), GUILayout.Width(50));
            GUI.color = sourceLiltoon ? Color.green : Color.red;
            EditorGUILayout.LabelField(sourceMaterial != null ? sourceMaterial.name : LocalizationManager.Get("not_selected"), EditorStyles.boldLabel);
            GUI.color = Color.white;
            EditorGUILayout.LabelField(sourceLiltoon ? "✓ liltoon" : "⚠ 非liltoon", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationManager.Get("transfer_target"), GUILayout.Width(50));
            GUI.color = targetLiltoon ? Color.green : Color.red;
            EditorGUILayout.LabelField(targetMaterial != null ? targetMaterial.name : LocalizationManager.Get("not_selected"), EditorStyles.boldLabel);
            GUI.color = Color.white;
            EditorGUILayout.LabelField(targetLiltoon ? "✓ liltoon" : "⚠ 非liltoon", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            if (!sourceLiltoon || !targetLiltoon)
            {
                EditorGUILayout.HelpBox(LocalizationManager.Get("liltoon_both_required"), MessageType.Warning);
            }
        }

        private void DrawDirectTabClassic()
        {
            EditorGUILayout.LabelField("直接指定", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox("💡 使い方: Hierarchyから色を変えたいGameObjectをドラッグ&ドロップしてください。\nメインカラーテクスチャ（_MainTex）が自動的に抽出され、色調整が行われます。", MessageType.Info);
            GUILayout.Space(10);

            DrawInputsResetControl();
            EditorGUILayout.LabelField(LocalizationManager.Get("texture_selection"), EditorStyles.boldLabel);

            // Target and reference GameObject selection
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            DrawDropAreaLabel("direct_target_texture", targetGameObject, selectedTargetMaterial);
            var newTargetGameObject = EditorGUILayout.ObjectField(
                targetGameObject,
                typeof(GameObject),
                true,
                GUILayout.MinWidth(160)
            ) as GameObject;

            if (newTargetGameObject != targetGameObject)
            {
                RestoreScenePreview();
                RestoreUncompressedTextureCache();
                targetGameObject = newTargetGameObject;
                targetComponent = GetRendererComponent(targetGameObject);
                selectedTargetMaterial = null; // Reset material selection

                if (previewTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(previewTexture, true);
                    previewTexture = null;
                }

                CheckForExperimentalAutoPreview();
                UpdateCurrentMeshInfo();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);
            GUILayout.Label("+", EditorStyles.boldLabel, GUILayout.Width(20));
            GUILayout.Space(8);

            EditorGUILayout.BeginVertical();
            DrawDropAreaLabel("direct_reference_texture", referenceGameObject, selectedReferenceMaterial);
            var newReferenceGameObject = EditorGUILayout.ObjectField(
                referenceGameObject,
                typeof(GameObject),
                true,
                GUILayout.MinWidth(160)
            ) as GameObject;

            if (newReferenceGameObject != referenceGameObject)
            {
                RestoreUncompressedTextureCache();
                referenceGameObject = newReferenceGameObject;
                referenceComponent = GetRendererComponent(referenceGameObject);
                selectedReferenceMaterial = null; // Reset material selection

                if (highPrecisionConfig != null)
                {
                    highPrecisionConfig.referenceGameObject = newReferenceGameObject;
                    ClearPreview();
                    uvUsageStats = "";

                    if (useHighPrecisionMode)
                    {
                        GenerateUVMaskPreview();
                    }
                }

                if (previewTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(previewTexture, true);
                    previewTexture = null;
                }
                CheckForExperimentalAutoPreview();
                UpdateCurrentMeshInfo();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (referenceGameObject == null || targetGameObject == null)
            {
                EditorGUILayout.HelpBox("Skinned Mesh RendererまたはMesh Rendererを持つGameObjectを選択してください", MessageType.Info);
            }

            if (referenceGameObject != null)
            {
                if (referenceComponent == null)
                {
                    EditorGUILayout.HelpBox("選択されたGameObjectにSkinned Mesh RendererまたはMesh Rendererが見つかりません", MessageType.Warning);
                }
                else
                {
                    // Show detected component info
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("検出されたコンポーネント:", GUILayout.Width(140));
                    EditorGUILayout.LabelField(referenceComponent.GetType().Name, EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();

                    // Show material selection for reference component
                    var oldReferenceMaterial = selectedReferenceMaterial;
                    DrawMaterialSelectionForComponent(referenceComponent, "参照用", ref selectedReferenceMaterial);

                    // Check if reference material changed and update UV mask preview
                    if (oldReferenceMaterial != selectedReferenceMaterial && useHighPrecisionMode)
                    {
                        GenerateUVMaskPreview();
                    }
                }
            }

            // High-precision mode section removed per design update

            // Dual color selection mode
            if (GetExperimentalReferenceTexture() != null && GetExperimentalTargetTexture() != null)
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField(LocalizationManager.Get("color_selection_mode"), EditorStyles.boldLabel);

                useDualColorSelection = EditorGUILayout.Toggle(LocalizationManager.Get("enable_color_selection"), useDualColorSelection);
                EditorGUILayout.HelpBox(LocalizationManager.Get("dual_color_selection_help"), MessageType.Info);

                if (useDualColorSelection)
                {
                    DrawDualColorSelectionUIForExperimental();
                }
            }

            // Visual flow indicator
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("↓", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(20));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (targetGameObject != null)
            {
                if (targetComponent == null)
                {
                    EditorGUILayout.HelpBox("選択されたGameObjectにSkinned Mesh RendererまたはMesh Rendererが見つかりません", MessageType.Warning);
                }
                else
                {
                    // Show detected component info
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("検出されたコンポーネント:", GUILayout.Width(140));
                    EditorGUILayout.LabelField(targetComponent.GetType().Name, EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();

                    // Show material selection for target component
                    DrawMaterialSelectionForComponent(targetComponent, "変更対象", ref selectedTargetMaterial);
                }
            }

            // Visual flow indicator for result
            if (previewTexture != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("↓ 適用結果 / Result", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            // Adjustment parameters
            EditorGUILayout.LabelField(LocalizationManager.Get("adjustment_parameters"), EditorStyles.boldLabel);

            adjustmentIntensity = EditorGUILayout.Slider(LocalizationManager.Get("adjustment_intensity"), adjustmentIntensity, 0f, 100f);
            preserveLuminance = EditorGUILayout.Toggle(LocalizationManager.Get("preserve_luminance"), preserveLuminance);

            // Custom popup for localized adjustment mode names
            string[] modeNames = LocalizationManager.GetColorAdjustmentModeDisplayNames();
            int selectedModeIndex = (int)adjustmentMode;
            selectedModeIndex = EditorGUILayout.Popup(LocalizationManager.Get("adjustment_mode"), selectedModeIndex, modeNames);
            adjustmentMode = (ColorAdjustmentMode)selectedModeIndex;

            GUILayout.Space(10);

            // Material transfer option
            if (selectedReferenceMaterial != null && selectedTargetMaterial != null)
            {
                EditorGUILayout.LabelField("マテリアル設定転送", EditorStyles.boldLabel);
                bool prevEnableMaterialTransfer = enableMaterialTransfer;
                enableMaterialTransfer = EditorGUILayout.Toggle("見え方も転送(マテリアル設定の転送)", enableMaterialTransfer);
                if (prevEnableMaterialTransfer != enableMaterialTransfer)
                    RefreshScenePreviewMaterial();

                if (enableMaterialTransfer)
                {
                    EditorGUILayout.HelpBox("💡 色調整と同時にliltoonのマテリアル設定（描画効果等）も転送されます。", MessageType.Info);

                    GUILayout.Space(5);
                    DrawMaterialTransferDirection();
                }

                GUILayout.Space(10);
            }

            DrawNDMFOptionsForDirectTab();

            GUILayout.Space(10);
            DrawSingleTextureColorControls();
            GUILayout.Space(10);

            // Preview options
            bool newShowPreviewClassic = EditorGUILayout.Toggle("ウィンドウ内プレビュー", showWindowPreview);
            if (newShowPreviewClassic != showWindowPreview)
            {
                showWindowPreview = newShowPreviewClassic;
                EditorPrefs.SetBool("TexColAdjuster_ShowWindowPreview", showWindowPreview);

                if (showWindowPreview && previewTexture == null && CanProcessExperimental())
                {
                    GenerateExperimentalPreview();
                }
            }

            if (showWindowPreview && previewTexture != null && GetExperimentalTargetTexture() != null)
            {
                DrawPreview();
            }

            // Action buttons
            DrawDirectTabActionButtons();

        }

        private void ApplyNDMFToSelectedMaterial()
        {
            if (!CanProcessExperimental())
                return;

            if (selectedTargetMaterial == null)
            {
                EditorUtility.DisplayDialog("エラー", "変更対象マテリアルを選択してください。", "OK");
                return;
            }

            var referenceTexture = GetExperimentalReferenceTexture();
            if (referenceTexture == null)
            {
                EditorUtility.DisplayDialog("エラー", "参照マテリアルからメインテクスチャを取得できませんでした。", "OK");
                return;
            }

            var renderer = targetComponent as Renderer;
            if (renderer == null)
            {
                EditorUtility.DisplayDialog("エラー", "選択されたコンポーネントはRendererではありません。", "OK");
                return;
            }

            // Temporarily restore original material so NDMF finds the correct slot
            bool wasPreviewActive = scenePreviewActive;
            if (wasPreviewActive) RestoreScenePreview();

            int materialSlot = NDMFIntegrationHelper.FindMaterialSlotWithMaterial(renderer, selectedTargetMaterial);
            bool dualSelectionActive = useDualColorSelection && hasSelectedTargetColor && hasSelectedReferenceColor;
            bool highPrecisionActive = useHighPrecisionMode && IsHighPrecisionConfigValidForDirectTab();

            var component = NDMFIntegrationHelper.AddOrUpdateComponent(
                renderer,
                materialSlot,
                referenceTexture,
                adjustmentMode,
                adjustmentIntensity,
                preserveLuminance,
                dualSelectionActive,
                selectedTargetColor,
                selectedReferenceColor,
                colorSelectionRange,
                hueShift,
                saturationMultiplier,
                brightnessOffset,
                gammaCorrection,
                midtoneShift,
                highPrecisionActive,
                highPrecisionActive ? highPrecisionConfig.referenceGameObject : null,
                highPrecisionConfig.materialIndex,
                highPrecisionConfig.uvChannel,
                highPrecisionConfig.dominantColorCount,
                highPrecisionConfig.useWeightedSampling);

            // Clear all window preview state — let NDMF handle preview from now on
            ClearPreview();

            if (component != null)
            {
                EditorUtility.DisplayDialog("完了", $"NDMFコンポーネントを '{renderer.gameObject.name}' に設定しました。", "OK");
                Selection.activeObject = component;
                EditorGUIUtility.PingObject(component);
            }
        }

        private void ApplyNDMFToAvatarRoot()
        {
            if (!CanProcessExperimental())
                return;

            if (targetGameObject == null || selectedTargetMaterial == null)
            {
                EditorUtility.DisplayDialog("エラー", "NDMF適用には対象GameObjectとマテリアルの選択が必要です。", "OK");
                return;
            }

            var referenceTexture = GetExperimentalReferenceTexture();
            if (referenceTexture == null)
            {
                EditorUtility.DisplayDialog("エラー", "参照マテリアルからメインテクスチャを取得できませんでした。", "OK");
                return;
            }

            // Temporarily restore original material so NDMF finds correct material references
            bool wasPreviewActive = scenePreviewActive;
            if (wasPreviewActive) RestoreScenePreview();

            bool dualSelectionActive = useDualColorSelection && hasSelectedTargetColor && hasSelectedReferenceColor;
            bool highPrecisionActive = useHighPrecisionMode && IsHighPrecisionConfigValidForDirectTab();

            NDMFIntegrationHelper.AddNDMFComponentsUnderAvatarRoot(
                targetGameObject,
                selectedTargetMaterial,
                referenceTexture,
                adjustmentMode,
                adjustmentIntensity,
                preserveLuminance,
                dualSelectionActive,
                selectedTargetColor,
                selectedReferenceColor,
                colorSelectionRange,
                0f,
                1f,
                1f,
                1f,
                highPrecisionActive,
                highPrecisionActive ? highPrecisionConfig.referenceGameObject : null,
                highPrecisionConfig.materialIndex,
                highPrecisionConfig.uvChannel,
                highPrecisionConfig.dominantColorCount,
                highPrecisionConfig.useWeightedSampling,
                includeInactiveObjects);

            // Clear all window preview state — let NDMF handle preview from now on
            ClearPreview();
        }

        private void DrawNDMFOptionsForDirectTab()
        {
            EditorGUILayout.LabelField(LocalizationManager.Get("ndmf_settings"), GetLargeBoldLabelStyle());
            EditorGUILayout.BeginVertical("box");
            var largeToggleStyle = GetLargeToggleLabelStyle();
            float toggleHeight = largeToggleStyle.fontSize + largeToggleStyle.padding.vertical + 6f;
            bool newValue = EditorGUILayout.ToggleLeft(new GUIContent(LocalizationManager.Get("ndmf_toggle_label")), useNDMFDirectMode, largeToggleStyle, GUILayout.Height(toggleHeight));
            if (newValue != useNDMFDirectMode)
            {
                useNDMFDirectMode = newValue;
                NDMFIntegrationHelper.SetNDMFMode("Direct", useNDMFDirectMode);
                Repaint();
            }

            if (useNDMFDirectMode)
            {
                EditorGUILayout.HelpBox(LocalizationManager.Get("ndmf_toggle_note"), MessageType.Info);
                includeInactiveObjects = EditorGUILayout.Toggle(LocalizationManager.Get("inactive_objects_toggle"), includeInactiveObjects);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSingleTextureColorControls()
        {
            EditorGUILayout.LabelField(LocalizationManager.Get("color_adjustment_controls"), EditorStyles.boldLabel);

            // Brightness adjustment
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Brightness:", GUILayout.Width(150));
            brightnessOffset = EditorGUILayout.Slider(brightnessOffset, -1f, 1f);
            EditorGUILayout.EndHorizontal();

            // Gamma correction (midpoint adjustment)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Gamma:", GUILayout.Width(150));
            gammaCorrection = EditorGUILayout.Slider(gammaCorrection, 0.2f, 5f);
            EditorGUILayout.EndHorizontal();

            // Midtone shift (histogram center shift)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Midtone Shift:", GUILayout.Width(150));
            midtoneShift = EditorGUILayout.Slider(midtoneShift, -0.5f, 0.5f);
            EditorGUILayout.EndHorizontal();
        }


        private void ResetSingleTextureAdjustments()
        {
            hueShift = 0f;
            saturationMultiplier = 1f;
            brightnessOffset = 0f;
            contrastMultiplier = 1f;
            gammaCorrection = 1f;
            midtoneShift = 0f;

            if (singleTexturePreview != null)
            {
                UnityEngine.Object.DestroyImmediate(singleTexturePreview, true);
                singleTexturePreview = null;
            }
        }

        private void DrawMaterialSelectionForComponent(Component component, string componentType, ref Material selectedMaterial)
        {
            var materials = ExtractMaterials(component);
            if (materials == null || materials.Length == 0)
            {
                EditorGUILayout.HelpBox($"{componentType}コンポーネントにマテリアルが見つかりません", MessageType.Warning);
                return;
            }

            if (materials.Length == 1)
            {
                var newMaterial = materials[0];
                if (selectedMaterial != newMaterial)
                {
                    selectedMaterial = newMaterial;
                    HandleDirectMaterialSelectionChanged();
                    CheckForExperimentalAutoPreview();
                }
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{componentType}マテリアル:", GUILayout.Width(100));
                EditorGUILayout.LabelField(selectedMaterial.name, EditorStyles.boldLabel);

                // Show liltoon status
                if (IsLiltoonMaterial(selectedMaterial))
                {
                    GUI.color = Color.green;
                    EditorGUILayout.LabelField("✓ liltoon", GUILayout.Width(60));
                }
                else
                {
                    GUI.color = Color.yellow;
                    EditorGUILayout.LabelField("⚠ 非liltoon", GUILayout.Width(60));
                }
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // Multiple materials - show selection popup
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{componentType}マテリアル:", GUILayout.Width(100));

                string[] materialNames = new string[materials.Length];
                int selectedIndex = -1;

                for (int i = 0; i < materials.Length; i++)
                {
                    materialNames[i] = $"{i}: {materials[i].name}";
                    if (materials[i] == selectedMaterial)
                        selectedIndex = i;
                }

                if (selectedIndex == -1 && materials.Length > 0)
                {
                    selectedIndex = 0;
                    var newMaterial = materials[0];
                    if (selectedMaterial != newMaterial)
                    {
                        selectedMaterial = newMaterial;
                        HandleDirectMaterialSelectionChanged();
                        CheckForExperimentalAutoPreview();
                    }
                }

                int newIndex = EditorGUILayout.Popup(selectedIndex, materialNames);
                if (newIndex != selectedIndex && newIndex >= 0 && newIndex < materials.Length)
                {
                    selectedMaterial = materials[newIndex];
                    HandleDirectMaterialSelectionChanged();
                    CheckForExperimentalAutoPreview();
                }

                EditorGUILayout.EndHorizontal();

                // Show selected material info
                if (selectedMaterial != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("", GUILayout.Width(100)); // Spacer
                    EditorGUILayout.LabelField($"選択中: {selectedMaterial.name}", EditorStyles.miniLabel);

                    // Show liltoon status
                    if (IsLiltoonMaterial(selectedMaterial))
                    {
                        GUI.color = Color.green;
                        EditorGUILayout.LabelField("✓ liltoon", EditorStyles.miniLabel, GUILayout.Width(60));
                    }
                    else
                    {
                        GUI.color = Color.yellow;
                        EditorGUILayout.LabelField("⚠ 非liltoon", EditorStyles.miniLabel, GUILayout.Width(60));
                    }
                    GUI.color = Color.white;
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void DrawDirectTabActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            float actionButtonHeight = EditorGUIUtility.singleLineHeight * 2f;

            bool canProcess = CanProcessExperimental();
            GUI.enabled = canProcess;

            if (useNDMFDirectMode)
            {
                if (GUILayout.Button(LocalizationManager.Get("apply_to_parts"), GUILayout.Height(actionButtonHeight)))
                {
                    ApplyNDMFToSelectedMaterial();
                }
                GUI.enabled = canProcess;
                if (GUILayout.Button(LocalizationManager.Get("ndmf_apply_all"), GUILayout.Height(actionButtonHeight)))
                {
                    ApplyNDMFToAvatarRoot();
                }
            }
            else
            {
                if (GUILayout.Button(LocalizationManager.Get("apply_adjustment"), GUILayout.Height(actionButtonHeight)))
                {
                    if (useHighPrecisionMode && GetExperimentalReferenceTexture() != null)
                    {
                        ApplyHighPrecisionAdjustmentForDirectTab();
                    }
                    else
                    {
                        ExecuteExperimentalColorAdjustment();
                    }
                }
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDropAreaLabel(string localizationKey, GameObject assignedGameObject, Material selectedMaterial)
        {
            Color previousContentColor = GUI.contentColor;

            if (assignedGameObject == null)
            {
                GUI.contentColor = DropLabelMissingColor;
            }
            else if (HasReadableMainTexture(assignedGameObject, selectedMaterial))
            {
                GUI.contentColor = DropLabelReadyColor;
            }

            EditorGUILayout.LabelField(LocalizationManager.Get(localizationKey), GetLargeBoldLabelStyle());
            GUI.contentColor = previousContentColor;
        }

        private void DrawTextureDropLabel(string localizationKey, Texture2D texture)
        {
            var previousContentColor = GUI.contentColor;
            GUI.contentColor = texture == null ? DropLabelMissingColor : DropLabelReadyColor;
            EditorGUILayout.LabelField(LocalizationManager.Get(localizationKey), GetLargeBoldLabelStyle());
            GUI.contentColor = previousContentColor;
        }

        private bool HasReadableMainTexture(GameObject gameObject, Material selectedMaterial)
        {
            if (gameObject == null)
                return false;

            if (selectedMaterial != null && GetMainTexture(selectedMaterial) != null)
                return true;

            var component = GetRendererComponent(gameObject);
            if (component == null)
                return false;

            var materials = ExtractMaterials(component);
            if (materials == null || materials.Length == 0)
                return false;

            foreach (var material in materials)
            {
                if (material != null && GetMainTexture(material) != null)
                    return true;
            }

            return false;
        }

        private bool HasParametersChanged()
        {
             return !Mathf.Approximately(lastAdjustmentIntensity, adjustmentIntensity) ||
                 lastPreserveLuminance != preserveLuminance ||
                 lastPreserveTexture != preserveTexture ||
                   lastAdjustmentMode != adjustmentMode ||
                   lastUseDualColorSelection != useDualColorSelection ||
                   lastSelectedTargetColor != selectedTargetColor ||
                   lastSelectedReferenceColor != selectedReferenceColor ||
                   lastHasSelectedTargetColor != hasSelectedTargetColor ||
                 lastHasSelectedReferenceColor != hasSelectedReferenceColor ||
                 !Mathf.Approximately(lastColorSelectionRange, colorSelectionRange) ||
                 lastUseHighPrecisionModeForPreview != useHighPrecisionMode ||
                 !Mathf.Approximately(lastHueShift, hueShift) ||
                 !Mathf.Approximately(lastSaturationMultiplier, saturationMultiplier) ||
                 !Mathf.Approximately(lastBrightnessOffset, brightnessOffset) ||
                 !Mathf.Approximately(lastContrastMultiplier, contrastMultiplier) ||
                 !Mathf.Approximately(lastGammaCorrection, gammaCorrection) ||
                 !Mathf.Approximately(lastMidtoneShift, midtoneShift);
        }

        private void UpdateParameterCache()
        {
            lastAdjustmentIntensity = adjustmentIntensity;
            lastPreserveLuminance = preserveLuminance;
            lastPreserveTexture = preserveTexture;
            lastAdjustmentMode = adjustmentMode;
            lastUseDualColorSelection = useDualColorSelection;
            lastSelectedTargetColor = selectedTargetColor;
            lastSelectedReferenceColor = selectedReferenceColor;
            lastHasSelectedTargetColor = hasSelectedTargetColor;
            lastHasSelectedReferenceColor = hasSelectedReferenceColor;
            lastColorSelectionRange = colorSelectionRange;
            lastUseHighPrecisionModeForPreview = useHighPrecisionMode;
            lastHueShift = hueShift;
            lastSaturationMultiplier = saturationMultiplier;
            lastBrightnessOffset = brightnessOffset;
            lastContrastMultiplier = contrastMultiplier;
            lastGammaCorrection = gammaCorrection;
            lastMidtoneShift = midtoneShift;
        }

        private readonly struct DirectPreviewParameterState : IEquatable<DirectPreviewParameterState>
        {
            public readonly float Intensity;
            public readonly bool PreserveLuminance;
            public readonly bool PreserveTexture;
            public readonly ColorAdjustmentMode Mode;
            public readonly bool DualColorSelection;
            public readonly Color TargetColor;
            public readonly Color ReferenceColor;
            public readonly bool HasTargetColor;
            public readonly bool HasReferenceColor;
            public readonly float SelectionRange;
            public readonly bool HighPrecision;
            public readonly float HueShift;
            public readonly float SaturationMultiplier;
            public readonly float BrightnessOffset;
            public readonly float ContrastMultiplier;
            public readonly float GammaCorrection;
            public readonly float MidtoneShift;

            public DirectPreviewParameterState(
                float intensity,
                bool preserveLuminance,
                bool preserveTexture,
                ColorAdjustmentMode mode,
                bool dualColorSelection,
                Color targetColor,
                Color referenceColor,
                bool hasTargetColor,
                bool hasReferenceColor,
                float selectionRange,
                bool highPrecision,
                float hueShift,
                float saturationMultiplier,
                float brightnessOffset,
                float contrastMultiplier,
                float gammaCorrection,
                float midtoneShift)
            {
                Intensity = intensity;
                PreserveLuminance = preserveLuminance;
                PreserveTexture = preserveTexture;
                Mode = mode;
                DualColorSelection = dualColorSelection;
                TargetColor = targetColor;
                ReferenceColor = referenceColor;
                HasTargetColor = hasTargetColor;
                HasReferenceColor = hasReferenceColor;
                SelectionRange = selectionRange;
                HighPrecision = highPrecision;
                HueShift = hueShift;
                SaturationMultiplier = saturationMultiplier;
                BrightnessOffset = brightnessOffset;
                ContrastMultiplier = contrastMultiplier;
                GammaCorrection = gammaCorrection;
                MidtoneShift = midtoneShift;
            }

            public bool Equals(DirectPreviewParameterState other)
            {
                return Mathf.Approximately(Intensity, other.Intensity) &&
                       PreserveLuminance == other.PreserveLuminance &&
                       PreserveTexture == other.PreserveTexture &&
                       Mode == other.Mode &&
                       DualColorSelection == other.DualColorSelection &&
                       TargetColor.Equals(other.TargetColor) &&
                       ReferenceColor.Equals(other.ReferenceColor) &&
                       HasTargetColor == other.HasTargetColor &&
                       HasReferenceColor == other.HasReferenceColor &&
                       Mathf.Approximately(SelectionRange, other.SelectionRange) &&
                       HighPrecision == other.HighPrecision &&
                       Mathf.Approximately(HueShift, other.HueShift) &&
                       Mathf.Approximately(SaturationMultiplier, other.SaturationMultiplier) &&
                       Mathf.Approximately(BrightnessOffset, other.BrightnessOffset) &&
                       Mathf.Approximately(ContrastMultiplier, other.ContrastMultiplier) &&
                       Mathf.Approximately(GammaCorrection, other.GammaCorrection) &&
                       Mathf.Approximately(MidtoneShift, other.MidtoneShift);
            }

            public override bool Equals(object obj)
            {
                return obj is DirectPreviewParameterState other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Intensity.GetHashCode();
                    hash = (hash * 397) ^ PreserveLuminance.GetHashCode();
                    hash = (hash * 397) ^ PreserveTexture.GetHashCode();
                    hash = (hash * 397) ^ (int)Mode;
                    hash = (hash * 397) ^ DualColorSelection.GetHashCode();
                    hash = (hash * 397) ^ TargetColor.GetHashCode();
                    hash = (hash * 397) ^ ReferenceColor.GetHashCode();
                    hash = (hash * 397) ^ HasTargetColor.GetHashCode();
                    hash = (hash * 397) ^ HasReferenceColor.GetHashCode();
                    hash = (hash * 397) ^ SelectionRange.GetHashCode();
                    hash = (hash * 397) ^ HighPrecision.GetHashCode();
                    hash = (hash * 397) ^ HueShift.GetHashCode();
                    hash = (hash * 397) ^ SaturationMultiplier.GetHashCode();
                    hash = (hash * 397) ^ BrightnessOffset.GetHashCode();
                    hash = (hash * 397) ^ ContrastMultiplier.GetHashCode();
                    hash = (hash * 397) ^ GammaCorrection.GetHashCode();
                    hash = (hash * 397) ^ MidtoneShift.GetHashCode();
                    return hash;
                }
            }
        }

        private Vector2Int CalculatePreviewSize(int width, int height, int maxSize)
        {
            if (width <= maxSize && height <= maxSize)
                return new Vector2Int(width, height);

            float aspectRatio = (float)width / height;

            if (width > height)
            {
                return new Vector2Int(maxSize, Mathf.RoundToInt(maxSize / aspectRatio));
            }
            else
            {
                return new Vector2Int(Mathf.RoundToInt(maxSize * aspectRatio), maxSize);
            }
        }

        private void DrawDualColorSelectionUI()
        {
            GUILayout.Space(5);

            // Instructions
            EditorGUILayout.HelpBox(LocalizationManager.Get("color_selection_instructions"), MessageType.Info);
            EditorGUILayout.LabelField(LocalizationManager.Get("color_selection_guide"), EditorStyles.miniLabel);

            GUILayout.Space(10);

            const float textureDisplaySize = 280f;

            // Side by side texture display
            EditorGUILayout.BeginHorizontal();

            // Target texture selection
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(LocalizationManager.Get("target_texture"), EditorStyles.boldLabel);

            targetTextureScrollPosition = EditorGUILayout.BeginScrollView(targetTextureScrollPosition, GUILayout.Height(textureDisplaySize + 20), GUILayout.Width(textureDisplaySize + 20));

            // Calculate display size for target texture
            float targetAspectRatio = (float)targetTexture.width / targetTexture.height;
            float targetDisplayWidth = textureDisplaySize;
            float targetDisplayHeight = textureDisplaySize / targetAspectRatio;

            if (targetDisplayHeight > textureDisplaySize)
            {
                targetDisplayHeight = textureDisplaySize;
                targetDisplayWidth = textureDisplaySize * targetAspectRatio;
            }

            Rect targetTextureRect = GUILayoutUtility.GetRect(targetDisplayWidth, targetDisplayHeight, GUILayout.Width(targetDisplayWidth), GUILayout.Height(targetDisplayHeight));
            EditorGUI.DrawTextureTransparent(targetTextureRect, targetTexture);
            HandleTargetTextureInput(targetTextureRect);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Reference texture selection
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(LocalizationManager.Get("reference_texture"), EditorStyles.boldLabel);

            referenceTextureScrollPosition = EditorGUILayout.BeginScrollView(referenceTextureScrollPosition, GUILayout.Height(textureDisplaySize + 20), GUILayout.Width(textureDisplaySize + 20));

            // Calculate display size for reference texture
            float refAspectRatio = (float)referenceTexture.width / referenceTexture.height;
            float refDisplayWidth = textureDisplaySize;
            float refDisplayHeight = textureDisplaySize / refAspectRatio;

            if (refDisplayHeight > textureDisplaySize)
            {
                refDisplayHeight = textureDisplaySize;
                refDisplayWidth = textureDisplaySize * refAspectRatio;
            }

            Rect refTextureRect = GUILayoutUtility.GetRect(refDisplayWidth, refDisplayHeight, GUILayout.Width(refDisplayWidth), GUILayout.Height(refDisplayHeight));

            // Draw texture with high precision mask if enabled
            if (useHighPrecisionMode && highPrecisionPreviewTexture != null)
            {
                EditorGUI.DrawTextureTransparent(refTextureRect, highPrecisionPreviewTexture);
            }
            else
            {
                EditorGUI.DrawTextureTransparent(refTextureRect, referenceTexture);
            }

            HandleReferenceTextureInput(refTextureRect);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Color preview section
            DrawDualColorPreview();
        }

        private void DrawDualColorSelectionUIForExperimental()
        {
            // Get main color textures from the selected materials
            var refTexture = GetExperimentalReferenceTexture();
            var targetTexture = GetExperimentalTargetTexture();

            if (refTexture == null || targetTexture == null)
                return;

            // Use the existing dual color selection UI with direct-specified textures
            var originalRefTexture = referenceTexture;
            var originalTargetTexture = this.targetTexture;

            // Temporarily set the main color textures for the dual color selection UI
            referenceTexture = refTexture;
            this.targetTexture = targetTexture;

            DrawDualColorSelectionUI();

            // Restore original textures
            referenceTexture = originalRefTexture;
            this.targetTexture = originalTargetTexture;
        }

        private Texture2D GetReadableTextureForPicking(Texture2D source, ref Texture2D cache, ref Texture2D cachedSource)
        {
            if (source == null)
            {
                return null;
            }

            if (cache != null && cachedSource == source)
            {
                return cache;
            }

            DisposeCachedTexture(ref cache, ref cachedSource);

            cachedSource = source;
            cache = TextureProcessor.MakeReadableCopy(source);
            return cache;
        }

        private void DisposeReadableTextureCaches()
        {
            DisposeCachedTexture(ref cachedTargetReadableForPicking, ref cachedTargetReadableSource);
            DisposeCachedTexture(ref cachedReferenceReadableForPicking, ref cachedReferenceReadableSource);
        }

        private static void DisposeCachedTexture(ref Texture2D cache, ref Texture2D cachedSource)
        {
            if (cache != null)
            {
                UnityEngine.Object.DestroyImmediate(cache, true);
                cache = null;
            }
            cachedSource = null;
        }

        private void HandleTargetTextureInput(Rect textureRect)
        {
            Event currentEvent = Event.current;
            Vector2 mousePosition = currentEvent.mousePosition;

            if (textureRect.Contains(mousePosition))
            {
                // Calculate UV coordinates
                Vector2 localMouse = mousePosition - textureRect.position;
                Vector2 uv = new Vector2(localMouse.x / textureRect.width, 1f - (localMouse.y / textureRect.height));

                // Clamp UV coordinates
                uv.x = Mathf.Clamp01(uv.x);
                uv.y = Mathf.Clamp01(uv.y);

                // Get pixel coordinates
                int pixelX = Mathf.FloorToInt(uv.x * targetTexture.width);
                int pixelY = Mathf.FloorToInt(uv.y * targetTexture.height);

                // Clamp pixel coordinates
                pixelX = Mathf.Clamp(pixelX, 0, targetTexture.width - 1);
                pixelY = Mathf.Clamp(pixelY, 0, targetTexture.height - 1);

                // Get color at pixel
                try
                {
                    var readableTexture = GetReadableTextureForPicking(targetTexture, ref cachedTargetReadableForPicking, ref cachedTargetReadableSource);
                    if (readableTexture == null)
                        return;

                    hoverTargetColor = readableTexture.GetPixel(pixelX, pixelY);

                    // Change cursor to eyedropper
                    EditorGUIUtility.AddCursorRect(textureRect, MouseCursor.FPS);

                    // Handle click
                    if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
                    {
                        selectedTargetColor = hoverTargetColor;
                        hasSelectedTargetColor = true;
                        currentEvent.Use();
                        Repaint();
                    }

                    // Repaint for hover color update
                    if (currentEvent.type == EventType.MouseMove)
                    {
                        Repaint();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to get target pixel color: {e.Message}");
                }
            }
        }

        private void HandleReferenceTextureInput(Rect textureRect)
        {
            Event currentEvent = Event.current;
            Vector2 mousePosition = currentEvent.mousePosition;

            if (textureRect.Contains(mousePosition))
            {
                // Calculate UV coordinates
                Vector2 localMouse = mousePosition - textureRect.position;
                Vector2 uv = new Vector2(localMouse.x / textureRect.width, 1f - (localMouse.y / textureRect.height));

                // Clamp UV coordinates
                uv.x = Mathf.Clamp01(uv.x);
                uv.y = Mathf.Clamp01(uv.y);

                // Get pixel coordinates
                int pixelX = Mathf.FloorToInt(uv.x * referenceTexture.width);
                int pixelY = Mathf.FloorToInt(uv.y * referenceTexture.height);

                // Clamp pixel coordinates
                pixelX = Mathf.Clamp(pixelX, 0, referenceTexture.width - 1);
                pixelY = Mathf.Clamp(pixelY, 0, referenceTexture.height - 1);

                // Get color at pixel
                try
                {
                    var readableTexture = GetReadableTextureForPicking(referenceTexture, ref cachedReferenceReadableForPicking, ref cachedReferenceReadableSource);
                    if (readableTexture == null && !(useHighPrecisionMode && highPrecisionConfig != null && highPrecisionConfig.referenceGameObject != null))
                        return;

                    // Use high precision mode color extraction if enabled
                    if (useHighPrecisionMode && highPrecisionConfig != null && highPrecisionConfig.referenceGameObject != null)
                    {
                        hoverReferenceColor = HighPrecisionProcessor.ExtractHighPrecisionTargetColor(
                            referenceTexture, highPrecisionConfig, uv);
                    }
                    else if (readableTexture != null)
                    {
                        hoverReferenceColor = readableTexture.GetPixel(pixelX, pixelY);
                    }

                    // Change cursor to eyedropper
                    EditorGUIUtility.AddCursorRect(textureRect, MouseCursor.FPS);

                    // Handle click
                    if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
                    {
                        selectedReferenceColor = hoverReferenceColor;
                        hasSelectedReferenceColor = true;
                        currentEvent.Use();
                        Repaint();
                    }

                    // Repaint for hover color update
                    if (currentEvent.type == EventType.MouseMove)
                    {
                        Repaint();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to get reference pixel color: {e.Message}");
                }
            }
        }

        private Texture2D GetExperimentalReferenceTexture()
        {
            // Extract the main color texture from the selected reference material
            if (selectedReferenceMaterial != null)
                return GetMainTexture(selectedReferenceMaterial);
            return null;
        }

        private Texture2D GetExperimentalTargetTexture()
        {
            if (selectedTargetMaterial != null)
                return GetMainTexture(selectedTargetMaterial);
            return null;
        }

        private bool CanProcessExperimental()
        {
            return referenceComponent != null && targetComponent != null &&
                   selectedReferenceMaterial != null && selectedTargetMaterial != null;
        }
    }
}
