using UnityEngine;
using UnityEditor;
using TexColAdjuster.Runtime;

namespace TexColAdjuster.Editor
{
    [CustomEditor(typeof(TextureColorAdjustmentComponent))]
    public class TextureColorAdjustmentComponentEditor : UnityEditor.Editor
    {
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;

        private void OnEnable()
        {
            InitializeStyles();
        }

        private void InitializeStyles()
        {
            _labelStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                fontSize = 11
            };

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(0, 0, 10, 5)
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var component = (TextureColorAdjustmentComponent)target;

            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            // Draw high precision mode status
            if (component.useHighPrecisionMode)
            {
                DrawHighPrecisionModeInfo(component);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHighPrecisionModeInfo(TextureColorAdjustmentComponent component)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            GUILayout.Label("✓ High Precision Mode Enabled", _headerStyle);

            EditorGUILayout.Space(5);

            // Reference Object Status
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Reference Object:", GUILayout.Width(120));
            if (component.highPrecisionReferenceObject != null)
            {
                GUILayout.Label($"✓ {component.highPrecisionReferenceObject.name}", _labelStyle);
            }
            else
            {
                GUILayout.Label("<color=orange>⚠ Not Set</color>", _labelStyle);
            }
            EditorGUILayout.EndHorizontal();

            // Material Index
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Material Index:", GUILayout.Width(120));
            GUILayout.Label($"{component.highPrecisionMaterialIndex}", _labelStyle);
            EditorGUILayout.EndHorizontal();

            // UV Channel
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("UV Channel:", GUILayout.Width(120));
            GUILayout.Label($"UV{component.highPrecisionUVChannel}", _labelStyle);
            EditorGUILayout.EndHorizontal();

            // Dominant Colors
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Dominant Colors:", GUILayout.Width(120));
            GUILayout.Label($"{component.highPrecisionDominantColorCount}", _labelStyle);
            EditorGUILayout.EndHorizontal();

            // Weighted Sampling
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Weighted Sampling:", GUILayout.Width(120));
            GUILayout.Label(component.highPrecisionUseWeightedSampling ? "✓ Enabled" : "Disabled", _labelStyle);
            EditorGUILayout.EndHorizontal();

            // Warnings
            if (component.highPrecisionReferenceObject == null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Reference Object is required for high precision mode to work properly.", MessageType.Warning);
            }
            else
            {
                // Check if the reference object has a Renderer
                var renderer = component.highPrecisionReferenceObject.GetComponent<Renderer>();
                if (renderer == null)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox("Reference Object does not have a Renderer component.", MessageType.Warning);
                }
                else if (component.highPrecisionMaterialIndex >= renderer.sharedMaterials.Length)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox($"Material index {component.highPrecisionMaterialIndex} is out of range. Object has {renderer.sharedMaterials.Length} material(s).", MessageType.Warning);
                }
            }

            EditorGUILayout.EndVertical();
        }
    }
}
