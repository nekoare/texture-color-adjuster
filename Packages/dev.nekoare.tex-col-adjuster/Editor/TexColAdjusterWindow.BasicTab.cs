using UnityEngine;
using UnityEditor;
using TexColAdjuster.Editor;
using ColorAdjustmentMode = TexColAdjuster.Runtime.ColorAdjustmentMode;

namespace TexColAdjuster
{
    public partial class TexColAdjusterWindow
    {
        private void DrawBasicTab()
        {
            if (useNewUI)
                DrawBasicTabNew();
            else
                DrawBasicTabClassic();
        }

        private void DrawBasicTabClassic()
        {
            DrawInputsResetControl();
            EditorGUILayout.LabelField(LocalizationManager.Get("texture_selection"), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            DrawTextureDropLabel("target_texture", targetTexture);
            var newTargetTexture = (Texture2D)EditorGUILayout.ObjectField(targetTexture, typeof(Texture2D), false, GUILayout.MinWidth(160));
            if (newTargetTexture != targetTexture)
            {
                targetTexture = newTargetTexture;
                ClearPreview();
                CheckForAutoPreview();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);
            GUILayout.Label("+", EditorStyles.boldLabel, GUILayout.Width(20));
            GUILayout.Space(8);

            EditorGUILayout.BeginVertical();
            DrawTextureDropLabel("direct_reference_texture", referenceTexture);
            var newReferenceTexture = (Texture2D)EditorGUILayout.ObjectField(referenceTexture, typeof(Texture2D), false, GUILayout.MinWidth(160));
            if (newReferenceTexture != referenceTexture)
            {
                referenceTexture = newReferenceTexture;
                ClearPreview();
                CheckForAutoPreview();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Processing parameters
            DrawProcessingParameters();

            GUILayout.Space(10);

            // Action buttons
            DrawActionButtons();

            GUILayout.Space(10);

            // Preview display
            if (previewTexture != null)
            {
                DrawTexColAdjusterPreview();
            }
        }

        private void DrawBasicTabNew()
        {
            // Step 1: Texture selection
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(LocalizationManager.Get("direct_target_texture"), EditorStyles.boldLabel);
            var newTarget = (Texture2D)EditorGUILayout.ObjectField(targetTexture, typeof(Texture2D), false);
            if (newTarget != targetTexture)
            {
                targetTexture = newTarget;
                ClearPreview();
                CheckForAutoPreview();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(LocalizationManager.Get("direct_reference_texture"), EditorStyles.boldLabel);
            var newRef = (Texture2D)EditorGUILayout.ObjectField(referenceTexture, typeof(Texture2D), false);
            if (newRef != referenceTexture)
            {
                referenceTexture = newRef;
                ClearPreview();
                CheckForAutoPreview();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Step 2: Adjustment
            DrawBasicTabAdjustment();

            GUILayout.Space(10);

            // Step 3: Advanced settings + Preview + Apply
            DrawBasicTabApply();
        }

        private void DrawBasicTabAdjustment()
        {
            bool canAdjust = CanProcess();

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
                brightnessOffset = 0f;
                gammaCorrection = 1f;
                saturationMultiplier = 1f;
                midtoneShift = 0f;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUI.enabled = true;
        }

        private void DrawBasicTabApply()
        {
            // Advanced settings (foldout)
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

                EditorGUILayout.EndVertical();
            }

            GUILayout.Space(5);

            // Reference / Adjusted preview
            if (referenceTexture != null && previewTexture != null)
            {
                DrawReferenceAndAdjustedPreview(referenceTexture, previewTexture);
            }

            GUILayout.Space(5);

            // Apply button
            bool canProcess = CanProcess();
            GUI.enabled = canProcess;
            float buttonHeight = EditorGUIUtility.singleLineHeight * 2f;
            if (GUILayout.Button(LocalizationManager.Get("apply_adjustment"), GUILayout.Height(buttonHeight)))
            {
                ApplyAdjustment();
            }
            GUI.enabled = true;
        }

        private void DrawReferenceAndAdjustedPreview(Texture refTex, Texture adjustedTex)
        {
            EditorGUILayout.LabelField(LocalizationManager.Get("preview"), EditorStyles.boldLabel);

            float availableWidth = EditorGUIUtility.currentViewWidth - 30f;
            float halfWidth = availableWidth / 2f;

            float aspectRatio = (float)adjustedTex.height / adjustedTex.width;
            float previewHeight = Mathf.Min(halfWidth * aspectRatio, 300f);

            EditorGUILayout.BeginHorizontal();

            // Left: Reference
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(LocalizationManager.Get("direct_reference_texture"), EditorStyles.centeredGreyMiniLabel);
            GUILayout.Label(refTex, GUILayout.Width(halfWidth), GUILayout.Height(previewHeight));
            EditorGUILayout.EndVertical();

            // Right: Adjusted
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(LocalizationManager.Get("adjusted"), EditorStyles.centeredGreyMiniLabel);
            GUILayout.Label(adjustedTex, GUILayout.Width(halfWidth), GUILayout.Height(previewHeight));
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawProcessingParameters()
        {
            EditorGUILayout.LabelField(LocalizationManager.Get("processing_parameters"), EditorStyles.boldLabel);

            // Intensity slider
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationManager.Get("intensity"), GUILayout.Width(200));
            intensity = EditorGUILayout.Slider(intensity, 0f, 200f);
            EditorGUILayout.EndHorizontal();

            // Preserve luminance
            preserveLuminance = EditorGUILayout.Toggle(LocalizationManager.Get("preserve_luminance"), preserveLuminance);

            // Color adjustment mode
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationManager.Get("adjustment_mode"), GUILayout.Width(200));
            colorAdjustmentMode = (ColorAdjustmentMode)EditorGUILayout.EnumPopup(colorAdjustmentMode);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            float buttonHeight = EditorGUIUtility.singleLineHeight * 1.5f;

            GUI.enabled = CanProcess();
            if (GUILayout.Button(LocalizationManager.Get("generate_preview"), GUILayout.Height(buttonHeight)))
            {
                GeneratePreview();
            }

            if (GUILayout.Button(LocalizationManager.Get("apply_adjustment"), GUILayout.Height(buttonHeight)))
            {
                ApplyAdjustment();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }
    }
}
