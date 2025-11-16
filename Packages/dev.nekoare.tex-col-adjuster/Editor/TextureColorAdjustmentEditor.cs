using UnityEditor;
using UnityEngine;
using TexColAdjuster.Runtime;

namespace TexColAdjuster.Editor
{
    [CustomEditor(typeof(TextureColorAdjustmentComponent))]
    [CanEditMultipleObjects]
    public class TextureColorAdjustmentEditor : UnityEditor.Editor
    {
        private SerializedProperty targetBindingsProp;
        private SerializedProperty referenceTextureProp;
        private SerializedProperty adjustmentModeProp;
        private SerializedProperty intensityProp;
        private SerializedProperty preserveLuminanceProp;
        private SerializedProperty useDualColorSelectionProp;
        private SerializedProperty targetColorProp;
        private SerializedProperty referenceColorProp;
        private SerializedProperty selectionRangeProp;
        private SerializedProperty applyDuringBuildProp;
        private SerializedProperty previewEnabledProp;
        private SerializedProperty previewOnCPUProp;
        private SerializedProperty hueShiftProp;
        private SerializedProperty saturationProp;
        private SerializedProperty brightnessProp;
        private SerializedProperty gammaProp;
        private bool previewSettingsFoldout;
        private bool targetSettingsFoldout;

        private void OnEnable()
        {
            targetBindingsProp = serializedObject.FindProperty("targetBindings");
            referenceTextureProp = serializedObject.FindProperty("referenceTexture");
            adjustmentModeProp = serializedObject.FindProperty("adjustmentMode");
            intensityProp = serializedObject.FindProperty("intensity");
            preserveLuminanceProp = serializedObject.FindProperty("preserveLuminance");
            useDualColorSelectionProp = serializedObject.FindProperty("useDualColorSelection");
            targetColorProp = serializedObject.FindProperty("targetColor");
            referenceColorProp = serializedObject.FindProperty("referenceColor");
            selectionRangeProp = serializedObject.FindProperty("selectionRange");
            applyDuringBuildProp = serializedObject.FindProperty("applyDuringBuild");
            previewEnabledProp = serializedObject.FindProperty("PreviewEnabled");
            previewOnCPUProp = serializedObject.FindProperty("PreviewOnCPU");
            hueShiftProp = serializedObject.FindProperty("hueShift");
            saturationProp = serializedObject.FindProperty("saturation");
            brightnessProp = serializedObject.FindProperty("brightness");
            gammaProp = serializedObject.FindProperty("gamma");
            previewSettingsFoldout = false;
            targetSettingsFoldout = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();

            var component = (TextureColorAdjustmentComponent)target;

            // Preview Controls Section
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            Rect previewFoldoutRect = EditorGUILayout.GetControlRect();
            previewSettingsFoldout = EditorGUI.Foldout(
                previewFoldoutRect,
                previewSettingsFoldout,
                LocalizationManager.Get("component_preview_controls"),
                true);

            EditorGUILayout.HelpBox(LocalizationManager.Get("component_preview_hint"), MessageType.None);
            DrawToggleLeft(previewEnabledProp, GetLocalizedContent("component_enable_preview", "component_enable_preview_tooltip"));

            if (previewSettingsFoldout)
            {
                EditorGUI.indentLevel++;
                DrawToggleLeft(previewOnCPUProp, GetLocalizedContent("component_preview_on_cpu", "component_preview_on_cpu_tooltip"));

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField(LocalizationManager.Get("component_settings"), EditorStyles.boldLabel);
                DrawToggleLeft(applyDuringBuildProp, new GUIContent(
                    LocalizationManager.Get("component_apply_on_build"),
                    LocalizationManager.Get("component_apply_on_build_tooltip")));

                EditorGUI.indentLevel--;
            }

            if (!previewEnabledProp.hasMultipleDifferentValues && previewEnabledProp.boolValue)
            {
                EditorGUILayout.HelpBox(LocalizationManager.Get("component_preview_active"), MessageType.Info);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // Target Settings
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Rect targetFoldoutRect = EditorGUILayout.GetControlRect();
            targetSettingsFoldout = EditorGUI.Foldout(
                targetFoldoutRect,
                targetSettingsFoldout,
                LocalizationManager.Get("component_target_settings"),
                true);

            if (targetSettingsFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(targetBindingsProp, new GUIContent("Renderer Bindings"), true);

                if (!component.HasValidBindings)
                {
                    EditorGUILayout.HelpBox("バインディングが設定されていません。対象レンダラーとマテリアルスロットを指定してください。", MessageType.Warning);
                }
                else
                {
                    foreach (var binding in component.EnumerateValidBindings())
                    {
                        var renderer = binding.renderer;
                        string rendererName = renderer != null ? renderer.name : "<missing renderer>";
                        string materialName = "<invalid slot>";

                        if (renderer != null)
                        {
                            var materials = renderer.sharedMaterials;
                            if (materials != null && binding.materialSlot >= 0 && binding.materialSlot < materials.Length)
                            {
                                var material = materials[binding.materialSlot];
                                if (material != null)
                                {
                                    materialName = material.name;
                                }
                                else
                                {
                                    materialName = "<null material>";
                                }
                            }
                        }

                        EditorGUILayout.LabelField($"- {rendererName} / Slot {binding.materialSlot} : {materialName}");
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // Reference Texture
            EditorGUILayout.LabelField(LocalizationManager.Get("component_reference_texture"), EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(referenceTextureProp, new GUIContent(LocalizationManager.Get("component_reference_texture")));
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // Adjustment Settings
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(adjustmentModeProp, new GUIContent(LocalizationManager.Get("component_adjustment_mode")));
            EditorGUILayout.PropertyField(intensityProp, new GUIContent(LocalizationManager.Get("component_intensity")));
            EditorGUILayout.PropertyField(preserveLuminanceProp, new GUIContent(LocalizationManager.Get("component_preserve_luminance")));
            GUILayout.Space(5);
            EditorGUILayout.PropertyField(hueShiftProp, GetLocalizedContent("component_post_adjustment_hue", "component_post_adjustment_hue_tooltip"));
            EditorGUILayout.PropertyField(saturationProp, GetLocalizedContent("component_post_adjustment_saturation", "component_post_adjustment_saturation_tooltip"));
            EditorGUILayout.PropertyField(brightnessProp, GetLocalizedContent("component_post_adjustment_brightness", "component_post_adjustment_brightness_tooltip"));
            EditorGUILayout.PropertyField(gammaProp, GetLocalizedContent("component_post_adjustment_gamma", "component_post_adjustment_gamma_tooltip"));
            // Small quick-adjust buttons for gamma
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("色を薄くする", GUILayout.Width(110)))
            {
                gammaProp.floatValue = Mathf.Clamp(gammaProp.floatValue - 0.5f, 0.1f, 5f);
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(component);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("少し薄く", GUILayout.Width(100)))
            {
                gammaProp.floatValue = Mathf.Clamp(gammaProp.floatValue - 0.1f, 0.1f, 5f);
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(component);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("少し濃く", GUILayout.Width(100)))
            {
                gammaProp.floatValue = Mathf.Clamp(gammaProp.floatValue + 0.1f, 0.1f, 5f);
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(component);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // Dual Color Selection
            EditorGUILayout.LabelField(LocalizationManager.Get("component_dual_color_selection"), EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(useDualColorSelectionProp, GetLocalizedContent("component_use_dual_color"));

            if (useDualColorSelectionProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(targetColorProp, new GUIContent(LocalizationManager.Get("component_target_color")));
                EditorGUILayout.PropertyField(referenceColorProp, new GUIContent(LocalizationManager.Get("component_reference_color")));
                EditorGUILayout.PropertyField(selectionRangeProp, new GUIContent(LocalizationManager.Get("component_selection_range")));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // Open Advanced Editor Button
            if (GUILayout.Button(LocalizationManager.Get("component_open_advanced_editor"), GUILayout.Height(30)))
            {
                // This will be implemented when EditorWindow integration is complete
                EditorUtility.DisplayDialog(
                    LocalizationManager.Get("component_advanced_editor_coming_soon"),
                    LocalizationManager.Get("component_advanced_editor_message"),
                    "OK"
                );
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawToggleLeft(SerializedProperty property, GUIContent content)
        {
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            Rect controlRect = EditorGUILayout.GetControlRect();
            bool newValue = EditorGUI.ToggleLeft(controlRect, content, property.boolValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.boolValue = newValue;
            }
            EditorGUI.showMixedValue = false;
        }

        private GUIContent GetLocalizedContent(string labelKey, string tooltipKey = null)
        {
            string label = LocalizationManager.Get(labelKey);
            string tooltip = tooltipKey != null ? LocalizationManager.Get(tooltipKey) : string.Empty;
            return new GUIContent(label, tooltip);
        }
    }
}
