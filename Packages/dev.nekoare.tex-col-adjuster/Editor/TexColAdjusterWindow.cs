using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace TexColAdjuster
{
    public class TexColAdjusterWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private int activeTab = 0;
        private readonly string[] tabs = { "Basic", "Direct", "Color Adjust", "Shader Settings" };
        
        // Texture references
        private Texture2D referenceTexture;
        private Texture2D targetTexture;
        private Texture2D previewTexture;
        private Texture2D originalTexture;
        
        // Color adjustment parameters
        private float adjustmentIntensity = 100f;
        private bool preserveLuminance = false;
        private bool preserveTexture = true;
        private ColorAdjustmentMode adjustmentMode = ColorAdjustmentMode.LabHistogramMatching;
        
        // UI state
        private bool showPreview = true;
        private bool realTimePreview = true;
        private bool isProcessing = false;
        private float processingProgress = 0f;
        
        // Dual color selection mode (replacing eyedropper)
        private bool useDualColorSelection = false;
        private Color selectedTargetColor = Color.white;
        private Color selectedReferenceColor = Color.white;
        private Color hoverTargetColor = Color.white;
        private Color hoverReferenceColor = Color.white;
        private bool hasSelectedTargetColor = false;
        private bool hasSelectedReferenceColor = false;
        private Vector2 targetTextureScrollPosition;
        private Vector2 referenceTextureScrollPosition;
        
        // Color selection range control
        private float colorSelectionRange = 1.0f; // Default to full selection range
        
        // Single texture color adjustments (for dedicated tab)
        private Texture2D singleTexture;
        private Texture2D singleTexturePreview;
        private float singleGammaAdjustment = 1.0f;
        private float singleSaturationAdjustment = 1.0f;
        private float singleBrightnessAdjustment = 1.0f;
        
        // Parameter change tracking
        private float lastAdjustmentIntensity = -1f;
        private bool lastPreserveLuminance = false;
        private bool lastPreserveTexture = true;
        private ColorAdjustmentMode lastAdjustmentMode = ColorAdjustmentMode.LabHistogramMatching;
        private bool lastUseDualColorSelection = false;
        private Color lastSelectedTargetColor = Color.white;
        private Color lastSelectedReferenceColor = Color.white;
        private bool lastHasSelectedTargetColor = false;
        private bool lastHasSelectedReferenceColor = false;
        private float lastColorSelectionRange = 0.3f;
        
        // Presets
        private List<ColorAdjustmentPreset> presets = new List<ColorAdjustmentPreset>();
        private int selectedPreset = 0;
        
        // Liltoon preset variables (simplified for 2-material workflow)
        private Material sourceLiltoonMaterial;
        private List<Material> targetLiltoonMaterials = new List<Material>();
        
        // Experimental tab variables
        private GameObject referenceGameObject;
        private GameObject targetGameObject;
        private Component referenceComponent;
        private Component targetComponent;
        private Material selectedReferenceMaterial;
        private Material selectedTargetMaterial;
        
        [MenuItem("Tools/TexColAdjuster")]
        public static void ShowWindow()
        {
            var window = GetWindow<TexColAdjusterWindow>("TexColAdjuster");
            window.minSize = new Vector2(400, 600);
            window.Show();
        }
        
        private void OnEnable()
        {
            LoadPresets();
        }
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            DrawHeader();
            DrawTabs();
            DrawTabContent();
            
            EditorGUILayout.EndScrollView();
            
            if (isProcessing)
            {
                DrawProgressBar();
            }
            
            // Handle real-time preview
            if (realTimePreview && Event.current.type == EventType.Repaint)
            {
                if (activeTab == 0 && CanProcess() && HasParametersChanged())
                {
                    GeneratePreview();
                    UpdateParameterCache();
                }
                else if (activeTab == 1 && CanProcessExperimental() && HasParametersChanged())
                {
                    GenerateExperimentalPreview();
                    UpdateParameterCache();
                }
            }
        }
        
        private void DrawHeader()
        {
            GUILayout.Space(10);
            
            // Language toggle button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(LocalizationManager.GetLanguageDisplayName(), GUILayout.Width(80)))
            {
                LocalizationManager.ToggleLanguage();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            EditorGUILayout.LabelField(LocalizationManager.Get("window_subtitle"), EditorStyles.boldLabel);
            GUILayout.Space(5);
            EditorGUILayout.LabelField(LocalizationManager.Get("window_description"), EditorStyles.miniLabel);
            GUILayout.Space(10);
        }
        
        private void DrawTabs()
        {
            string[] localizedTabs = new string[]
            {
                LocalizationManager.Get("tab_basic"),
                LocalizationManager.Get("tab_direct"),
                LocalizationManager.Get("tab_color_adjust"),
                LocalizationManager.Get("tab_shader_settings")
            };
            
            activeTab = GUILayout.Toolbar(activeTab, localizedTabs);
            GUILayout.Space(10);
        }
        
        private void DrawTabContent()
        {
            switch (activeTab)
            {
                case 0:
                    DrawBasicTab();
                    break;
                case 1:
                    DrawDirectTab();
                    break;
                case 2:
                    DrawColorAdjustTab();
                    break;
                case 3:
                    DrawShaderSettingsTab();
                    break;
            }
        }
        
        private void DrawBasicTab()
        {
            EditorGUILayout.LabelField(LocalizationManager.Get("texture_selection"), EditorStyles.boldLabel);
            
            // Reference texture
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationManager.Get("reference_texture"), GUILayout.Width(200));
            var newReferenceTexture = (Texture2D)EditorGUILayout.ObjectField(referenceTexture, typeof(Texture2D), false);
            if (newReferenceTexture != referenceTexture)
            {
                referenceTexture = newReferenceTexture;
                // Clear existing preview when texture changes
                if (previewTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(previewTexture);
                    previewTexture = null;
                }
                CheckForAutoPreview();
            }
            EditorGUILayout.EndHorizontal();
            
            // Reference texture help text
            if (referenceTexture == null)
            {
                EditorGUILayout.HelpBox(LocalizationManager.Get("help_reference_texture"), MessageType.Info);
            }
            
            // Dual color selection mode
            if (referenceTexture != null && targetTexture != null)
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField(LocalizationManager.Get("color_selection_mode"), EditorStyles.boldLabel);
                
                useDualColorSelection = EditorGUILayout.Toggle(LocalizationManager.Get("enable_color_selection"), useDualColorSelection);
                EditorGUILayout.HelpBox(LocalizationManager.Get("dual_color_selection_help"), MessageType.Info);
                
                if (useDualColorSelection)
                {
                    DrawDualColorSelectionUI();
                }
            }
            
            // Visual flow indicator
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("↓", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(20));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            // Target texture
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationManager.Get("target_texture"), GUILayout.Width(200));
            var newTargetTexture = (Texture2D)EditorGUILayout.ObjectField(targetTexture, typeof(Texture2D), false);
            if (newTargetTexture != targetTexture)
            {
                targetTexture = newTargetTexture;
                // Clear existing preview when texture changes
                if (previewTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(previewTexture);
                    previewTexture = null;
                }
                CheckForAutoPreview();
            }
            EditorGUILayout.EndHorizontal();
            
            // Target texture help text
            if (targetTexture == null)
            {
                EditorGUILayout.HelpBox(LocalizationManager.Get("help_target_texture"), MessageType.Info);
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
            preserveTexture = EditorGUILayout.Toggle(LocalizationManager.Get("preserve_texture"), preserveTexture);
            
            // Custom popup for localized adjustment mode names
            string[] modeNames = LocalizationManager.GetColorAdjustmentModeDisplayNames();
            int selectedModeIndex = (int)adjustmentMode;
            selectedModeIndex = EditorGUILayout.Popup(LocalizationManager.Get("adjustment_mode"), selectedModeIndex, modeNames);
            adjustmentMode = (ColorAdjustmentMode)selectedModeIndex;
            
            GUILayout.Space(10);
            
            // Preview controls
            EditorGUILayout.LabelField(LocalizationManager.Get("preview"), EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            showPreview = EditorGUILayout.Toggle(LocalizationManager.Get("show_preview"), showPreview);
            realTimePreview = EditorGUILayout.Toggle(LocalizationManager.Get("realtime_preview"), realTimePreview);
            EditorGUILayout.EndHorizontal();
            
            if (realTimePreview)
            {
                EditorGUILayout.HelpBox("💡 動作が重い場合はリアルタイムプレビューをオフにしてください", MessageType.Info);
            }
            
            if (showPreview && targetTexture != null)
            {
                DrawPreview();
            }
            
            GUILayout.Space(10);
            
            // Action buttons
            DrawActionButtons();
        }
        
        private void DrawColorAdjustTab()
        {
            EditorGUILayout.LabelField(LocalizationManager.Get("single_texture_color_adjust"), EditorStyles.boldLabel);
            
            GUILayout.Space(10);
            
            // Single texture selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationManager.Get("select_texture"), GUILayout.Width(200));
            var newSingleTexture = (Texture2D)EditorGUILayout.ObjectField(singleTexture, typeof(Texture2D), false);
            if (newSingleTexture != singleTexture)
            {
                singleTexture = newSingleTexture;
                singleTexturePreview = null; // Reset preview
            }
            EditorGUILayout.EndHorizontal();
            
            if (singleTexture == null)
            {
                EditorGUILayout.HelpBox(LocalizationManager.Get("help_single_texture"), MessageType.Info);
                return;
            }
            
            GUILayout.Space(10);
            
            // Color adjustment controls
            EditorGUILayout.LabelField(LocalizationManager.Get("color_adjustments"), EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical("box");
            
            // Gamma
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationManager.Get("gamma_adjustment"), GUILayout.Width(80));
            singleGammaAdjustment = EditorGUILayout.Slider(singleGammaAdjustment, 0.1f, 3.0f);
            if (GUILayout.Button("1.0", GUILayout.Width(30)))
            {
                singleGammaAdjustment = 1.0f;
            }
            EditorGUILayout.EndHorizontal();
            
            // Saturation
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationManager.Get("saturation_adjustment"), GUILayout.Width(80));
            singleSaturationAdjustment = EditorGUILayout.Slider(singleSaturationAdjustment, 0.0f, 2.0f);
            if (GUILayout.Button("1.0", GUILayout.Width(30)))
            {
                singleSaturationAdjustment = 1.0f;
            }
            EditorGUILayout.EndHorizontal();
            
            // Brightness
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationManager.Get("brightness_adjustment"), GUILayout.Width(80));
            singleBrightnessAdjustment = EditorGUILayout.Slider(singleBrightnessAdjustment, 0.0f, 2.0f);
            if (GUILayout.Button("1.0", GUILayout.Width(30)))
            {
                singleBrightnessAdjustment = 1.0f;
            }
            EditorGUILayout.EndHorizontal();
            
            // Reset all button
            GUILayout.Space(5);
            if (GUILayout.Button(LocalizationManager.Get("reset_adjustments")))
            {
                singleGammaAdjustment = 1.0f;
                singleSaturationAdjustment = 1.0f;
                singleBrightnessAdjustment = 1.0f;
                singleTexturePreview = null;
            }
            
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);
            
            // Action buttons
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button(LocalizationManager.Get("generate_preview")))
            {
                GenerateSingleTexturePreview();
            }
            
            GUI.enabled = singleTexturePreview != null;
            if (GUILayout.Button(LocalizationManager.Get("apply_adjustment")))
            {
                ApplySingleTextureAdjustment();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Preview display
            if (singleTexturePreview != null)
            {
                DrawSingleTexturePreview();
            }
        }
        
        private void DrawDirectTab()
        {
            EditorGUILayout.LabelField("直接指定", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox("💡 使い方: Hierarchyから色を変えたいGameObjectをドラッグ&ドロップしてください。\nメインカラーテクスチャ（_MainTex）が自動的に抽出され、色調整が行われます。", MessageType.Info);
            GUILayout.Space(10);
            
            EditorGUILayout.LabelField(LocalizationManager.Get("texture_selection"), EditorStyles.boldLabel);
            
            // Reference component/texture selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationManager.Get("reference_texture"), GUILayout.Width(200));
            var newReferenceGameObject = EditorGUILayout.ObjectField(
                referenceGameObject, 
                typeof(GameObject), 
                true
            ) as GameObject;
            
            if (newReferenceGameObject != referenceGameObject)
            {
                referenceGameObject = newReferenceGameObject;
                referenceComponent = GetRendererComponent(referenceGameObject);
                selectedReferenceMaterial = null; // Reset material selection
                // Clear existing preview when component changes
                if (previewTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(previewTexture);
                    previewTexture = null;
                }
                CheckForExperimentalAutoPreview();
            }
            EditorGUILayout.EndHorizontal();
            
            if (referenceGameObject == null)
            {
                EditorGUILayout.HelpBox("Skinned Mesh RendererまたはMesh Rendererを持つGameObjectを選択してください", MessageType.Info);
            }
            else if (referenceComponent == null)
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
                DrawMaterialSelectionForComponent(referenceComponent, "参照用", ref selectedReferenceMaterial);
            }
            
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
            
            // Target component/texture selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationManager.Get("target_texture"), GUILayout.Width(200));
            var newTargetGameObject = EditorGUILayout.ObjectField(
                targetGameObject, 
                typeof(GameObject), 
                true
            ) as GameObject;
            
            if (newTargetGameObject != targetGameObject)
            {
                targetGameObject = newTargetGameObject;
                targetComponent = GetRendererComponent(targetGameObject);
                selectedTargetMaterial = null; // Reset material selection
                // Clear existing preview when component changes
                if (previewTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(previewTexture);
                    previewTexture = null;
                }
                CheckForExperimentalAutoPreview();
            }
            EditorGUILayout.EndHorizontal();
            
            if (targetGameObject == null)
            {
                EditorGUILayout.HelpBox("Skinned Mesh RendererまたはMesh Rendererを持つGameObjectを選択してください", MessageType.Info);
            }
            else if (targetComponent == null)
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
            preserveTexture = EditorGUILayout.Toggle(LocalizationManager.Get("preserve_texture"), preserveTexture);
            
            // Custom popup for localized adjustment mode names
            string[] modeNames = LocalizationManager.GetColorAdjustmentModeDisplayNames();
            int selectedModeIndex = (int)adjustmentMode;
            selectedModeIndex = EditorGUILayout.Popup(LocalizationManager.Get("adjustment_mode"), selectedModeIndex, modeNames);
            adjustmentMode = (ColorAdjustmentMode)selectedModeIndex;
            
            GUILayout.Space(10);
            
            // Preview controls
            EditorGUILayout.LabelField(LocalizationManager.Get("preview"), EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            showPreview = EditorGUILayout.Toggle(LocalizationManager.Get("show_preview"), showPreview);
            realTimePreview = EditorGUILayout.Toggle(LocalizationManager.Get("realtime_preview"), realTimePreview);
            EditorGUILayout.EndHorizontal();
            
            if (realTimePreview)
            {
                EditorGUILayout.HelpBox("💡 動作が重い場合はリアルタイムプレビューをオフにしてください", MessageType.Info);
            }
            
            if (showPreview && GetExperimentalTargetTexture() != null)
            {
                DrawPreview();
            }
            
            GUILayout.Space(10);
            
            // Action buttons
            DrawExperimentalActionButtons();
        }
        
        private void DrawPreview()
        {
            if (previewTexture != null)
            {
                float aspectRatio = (float)previewTexture.width / previewTexture.height;
                float previewHeight = 200f;
                float previewWidth = previewHeight * aspectRatio;
                
                GUILayout.Space(10);
                EditorGUILayout.LabelField(LocalizationManager.Get("preview"), EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                
                // Original
                if (originalTexture != null)
                {
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField(LocalizationManager.Get("original"), EditorStyles.centeredGreyMiniLabel);
                    GUILayout.Label(originalTexture, GUILayout.Width(previewWidth/2), GUILayout.Height(previewHeight/2));
                    EditorGUILayout.EndVertical();
                }
                
                // Adjusted
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(LocalizationManager.Get("adjusted"), EditorStyles.centeredGreyMiniLabel);
                GUILayout.Label(previewTexture, GUILayout.Width(previewWidth/2), GUILayout.Height(previewHeight/2));
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = CanProcess();
            if (GUILayout.Button(LocalizationManager.Get("generate_preview")))
            {
                GeneratePreview();
            }
            
            if (GUILayout.Button(LocalizationManager.Get("apply_adjustment")))
            {
                ApplyAdjustment();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawProgressBar()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(LocalizationManager.Get("processing"), EditorStyles.boldLabel);
            
            Rect progressRect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(progressRect, processingProgress, LocalizationManager.GetFormattedString("processing_status", processingProgress.ToString("P0")));
        }
        
        private bool CanProcess()
        {
            return referenceTexture != null && targetTexture != null && !isProcessing;
        }
        
        private void GeneratePreview()
        {
            if (!CanProcess()) return;
            
            isProcessing = true;
            processingProgress = 0f;
            
            try
            {
                // Ensure both textures are readable before processing
                var readableTarget = TextureProcessor.MakeTextureReadable(targetTexture);
                var readableReference = TextureProcessor.MakeTextureReadable(referenceTexture);
                
                // Validate textures and log format information
                Debug.Log($"Target texture: {targetTexture.name}, Format: {readableTarget.format}, Size: {readableTarget.width}x{readableTarget.height}");
                Debug.Log($"Reference texture: {referenceTexture.name}, Format: {readableReference.format}, Size: {readableReference.width}x{readableReference.height}");
                
                if (!TextureProcessor.ValidateTexture(readableTarget))
                {
                    throw new Exception($"Target texture '{targetTexture.name}' (format: {readableTarget.format}) is not readable or corrupted");
                }
                if (!TextureProcessor.ValidateTexture(readableReference))
                {
                    throw new Exception($"Reference texture '{referenceTexture.name}' (format: {readableReference.format}) is not readable or corrupted");
                }
                
                processingProgress = 0.2f;
                Repaint();
                
                // Resize textures for preview performance (max 512x512)
                const int maxPreviewSize = 512;
                var previewTarget = readableTarget;
                var previewReference = readableReference;
                
                if (readableTarget.width > maxPreviewSize || readableTarget.height > maxPreviewSize)
                {
                    var targetSize = CalculatePreviewSize(readableTarget.width, readableTarget.height, maxPreviewSize);
                    previewTarget = TextureProcessor.ResizeTexture(readableTarget, targetSize.x, targetSize.y);
                }
                
                if (readableReference.width > maxPreviewSize || readableReference.height > maxPreviewSize)
                {
                    var referenceSize = CalculatePreviewSize(readableReference.width, readableReference.height, maxPreviewSize);
                    previewReference = TextureProcessor.ResizeTexture(readableReference, referenceSize.x, referenceSize.y);
                }
                
                // Store original for comparison (use resized version for preview)
                if (originalTexture == null)
                {
                    originalTexture = DuplicateTexture(previewTarget);
                }
                
                processingProgress = 0.4f;
                Repaint();
                
                // Generate preview with resized textures for better performance
                if (useDualColorSelection && hasSelectedTargetColor && hasSelectedReferenceColor)
                {
                    previewTexture = ColorAdjuster.AdjustColorsWithDualSelection(
                        previewTarget, 
                        previewReference,
                        selectedTargetColor,
                        selectedReferenceColor,
                        adjustmentIntensity / 100f,
                        preserveLuminance,
                        adjustmentMode,
                        colorSelectionRange
                    );
                }
                else
                {
                    previewTexture = ColorAdjuster.AdjustColors(
                        previewTarget, 
                        previewReference, 
                        adjustmentIntensity / 100f,
                        preserveLuminance,
                        adjustmentMode
                    );
                }
                
                
                // Clean up resized textures if they were created
                if (previewTarget != readableTarget)
                    UnityEngine.Object.DestroyImmediate(previewTarget);
                if (previewReference != readableReference)
                    UnityEngine.Object.DestroyImmediate(previewReference);
                
                processingProgress = 1f;
                showPreview = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error generating preview: {e.Message}");
                EditorUtility.DisplayDialog(LocalizationManager.Get("error_title"), LocalizationManager.GetFormattedString("error_preview_message", e.Message), LocalizationManager.Get("ok"));
            }
            finally
            {
                isProcessing = false;
                Repaint();
            }
        }
        
        private void ApplyAdjustment()
        {
            if (!CanProcess()) return;
            
            if (previewTexture == null)
            {
                EditorUtility.DisplayDialog(LocalizationManager.Get("no_preview_title"), LocalizationManager.Get("no_preview_message"), LocalizationManager.Get("ok"));
                return;
            }
            
            string originalPath = AssetDatabase.GetAssetPath(targetTexture);
            
            var saveOptions = SaveOptionsExtensions.GetDefaultVRChatSettings();
            
            // Use DisplayDialogComplex to include cancel option
            int dialogResult = EditorUtility.DisplayDialogComplex(
                LocalizationManager.Get("apply_adjustment_title"), 
                LocalizationManager.Get("apply_adjustment_message"), 
                LocalizationManager.Get("save_as_new"), 
                LocalizationManager.Get("overwrite_original"),
                LocalizationManager.Get("cancel")
            );
            
            // Handle dialog result: 0 = save as new, 1 = overwrite, 2 = cancel
            // Note: Unity's DisplayDialogComplex may return different values depending on version
            Debug.Log($"[ApplyAdjustment] Dialog result: {dialogResult}");
            
            // Check for cancel first (including any non-zero, non-one values)
            if (dialogResult != 0 && dialogResult != 1)
            {
                Debug.Log($"[ApplyAdjustment] User cancelled (result: {dialogResult}) - exiting");
                return; // Exit without processing
            }
            
            if (dialogResult == 0) // Save as new
            {
                Debug.Log("[ApplyAdjustment] User chose to save as new file");
                saveOptions.overwriteOriginal = false;
            }
            else if (dialogResult == 1) // Overwrite
            {
                Debug.Log("[ApplyAdjustment] User chose to overwrite original file");
                saveOptions.overwriteOriginal = true;
            }
            
            // Process at full resolution for final output
            isProcessing = true;
            processingProgress = 0f;
            
            try
            {
                // Ensure both textures are readable before processing
                var readableTarget = TextureProcessor.MakeTextureReadable(targetTexture);
                var readableReference = TextureProcessor.MakeTextureReadable(referenceTexture);
                
                processingProgress = 0.3f;
                Repaint();
                
                // Generate full resolution result
                Texture2D fullResolutionResult;
                if (useDualColorSelection && hasSelectedTargetColor && hasSelectedReferenceColor)
                {
                    fullResolutionResult = ColorAdjuster.AdjustColorsWithDualSelection(
                        readableTarget, 
                        readableReference,
                        selectedTargetColor,
                        selectedReferenceColor,
                        adjustmentIntensity / 100f,
                        preserveLuminance,
                        adjustmentMode,
                        colorSelectionRange
                    );
                }
                else
                {
                    fullResolutionResult = ColorAdjuster.AdjustColors(
                        readableTarget, 
                        readableReference, 
                        adjustmentIntensity / 100f,
                        preserveLuminance,
                        adjustmentMode
                    );
                }
                
                
                processingProgress = 0.8f;
                Repaint();
                
                if (fullResolutionResult != null)
                {
                    if (TextureExporter.SaveTexture(fullResolutionResult, originalPath, saveOptions))
                    {
                        EditorUtility.DisplayDialog(LocalizationManager.Get("success_title"), LocalizationManager.Get("success_message"), LocalizationManager.Get("ok"));
                    }
                    else
                    {
                        EditorUtility.DisplayDialog(LocalizationManager.Get("error_title"), LocalizationManager.Get("error_save_message"), LocalizationManager.Get("ok"));
                    }
                    
                    UnityEngine.Object.DestroyImmediate(fullResolutionResult);
                }
                else
                {
                    EditorUtility.DisplayDialog(LocalizationManager.Get("error_title"), LocalizationManager.Get("error_save_message"), LocalizationManager.Get("ok"));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error applying adjustment: {e.Message}");
                EditorUtility.DisplayDialog(LocalizationManager.Get("error_title"), LocalizationManager.GetFormattedString("error_save_message"), LocalizationManager.Get("ok"));
            }
            finally
            {
                isProcessing = false;
                Repaint();
            }
        }
        
        private Texture2D DuplicateTexture(Texture2D source)
        {
            if (source == null) return null;
            
            // Ensure texture is readable before trying to get pixels
            var readableTexture = TextureProcessor.MakeTextureReadable(source);
            return TextureUtils.CreateCopy(readableTexture);
        }
        
        private void LoadPresets()
        {
            // Load presets from EditorPrefs or asset files
            presets.Clear();
            presets.Add(new ColorAdjustmentPreset(LocalizationManager.Get("preset_anime"), 75f, true, ColorAdjustmentMode.LabHistogramMatching));
            presets.Add(new ColorAdjustmentPreset(LocalizationManager.Get("preset_realistic"), 50f, true, ColorAdjustmentMode.LabHistogramMatching));
            presets.Add(new ColorAdjustmentPreset(LocalizationManager.Get("preset_soft"), 25f, true, ColorAdjustmentMode.LabHistogramMatching));
        }
        
        private bool ValidateComponent(Component component, string componentType)
        {
            if (component == null) return false;
            
            if (!(component is SkinnedMeshRenderer) && !(component is MeshRenderer))
            {
                EditorUtility.DisplayDialog(
                    "コンポーネントエラー", 
                    $"{componentType}コンポーネントはSkinned Mesh RendererまたはMesh Rendererである必要があります。", 
                    "OK"
                );
                return false;
            }
            
            return true;
        }
        
        private bool CanProcessExperimental()
        {
            return referenceComponent != null && targetComponent != null &&
                   selectedReferenceMaterial != null && selectedTargetMaterial != null;
        }
        
        private void ExecuteExperimentalColorAdjustment()
        {
            if (!CanProcessExperimental()) return;
            
            try
            {
                // Check if materials are liltoon
                if (!IsLiltoonMaterial(selectedReferenceMaterial) || !IsLiltoonMaterial(selectedTargetMaterial))
                {
                    int dialogResult = EditorUtility.DisplayDialogComplex(
                        "警告", 
                        "選択されたマテリアルがliltoonではありません。処理を続行しますか？",
                        "続行", "キャンセル", ""
                    );
                    // Handle dialog result: 0 = continue, 1 = cancel, -1 = closed with X button
                    if (dialogResult != 0) return; // Cancel or closed with X
                }
                
                // Extract textures from materials
                var referenceTexture = GetMainTexture(selectedReferenceMaterial);
                var targetTexture = GetMainTexture(selectedTargetMaterial);
                
                if (referenceTexture == null || targetTexture == null)
                {
                    EditorUtility.DisplayDialog("エラー", "テクスチャの抽出に失敗しました。メインテクスチャが設定されていない可能性があります。", "OK");
                    return;
                }
                
                // Apply existing color adjustment process
                ApplyColorAdjustmentToMaterial(referenceTexture, targetTexture, selectedTargetMaterial);
            }
            catch (Exception e)
            {
                Debug.LogError($"Experimental color adjustment failed: {e.Message}");
                EditorUtility.DisplayDialog("エラー", $"処理中にエラーが発生しました：{e.Message}", "OK");
            }
        }
        
        private Material[] ExtractMaterials(Component renderer)
        {
            if (renderer is SkinnedMeshRenderer smr)
                return smr.sharedMaterials;
            else if (renderer is MeshRenderer mr)
                return mr.sharedMaterials;
            return null;
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
                selectedMaterial = materials[0];
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
                    selectedMaterial = materials[0];
                }
                
                int newIndex = EditorGUILayout.Popup(selectedIndex, materialNames);
                if (newIndex != selectedIndex && newIndex >= 0 && newIndex < materials.Length)
                {
                    selectedMaterial = materials[newIndex];
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
        
        private bool IsLiltoonMaterial(Material material)
        {
            return material.shader.name.Contains("lilToon") || material.shader.name.Contains("lil/");
        }
        
        private Texture2D GetMainTexture(Material material)
        {
            // Get the main color texture (_MainTex) from the material
            // This is typically the base color/albedo texture used for the material
            return material.GetTexture("_MainTex") as Texture2D;
        }
        
        private Component GetRendererComponent(GameObject gameObject)
        {
            if (gameObject == null) return null;
            
            // Check for SkinnedMeshRenderer first
            var skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer != null)
                return skinnedMeshRenderer;
            
            // Check for MeshRenderer
            var meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                return meshRenderer;
            
            return null;
        }
        
        private string GetSavePathForTexture(string originalPath, SaveOptions options)
        {
            string directory = System.IO.Path.GetDirectoryName(originalPath);
            string filename = System.IO.Path.GetFileNameWithoutExtension(originalPath);
            string extension = GetExtensionFromFormat(options.format);
            
            if (options.overwriteOriginal)
            {
                return originalPath;
            }
            else
            {
                return System.IO.Path.Combine(directory, filename + "_adjusted" + extension);
            }
        }
        
        private string GetExtensionFromFormat(TextureExportFormat format)
        {
            switch (format)
            {
                case TextureExportFormat.PNG:
                    return ".png";
                case TextureExportFormat.JPG:
                    return ".jpg";
                case TextureExportFormat.TGA:
                    return ".tga";
                default:
                    return ".png";
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
            // Extract the main color texture from the selected target material
            if (selectedTargetMaterial != null)
                return GetMainTexture(selectedTargetMaterial);
            return null;
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
        
        private void DrawExperimentalActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = CanProcessExperimental();
            if (GUILayout.Button(LocalizationManager.Get("generate_preview")))
            {
                GenerateExperimentalPreview();
            }
            
            if (GUILayout.Button(LocalizationManager.Get("apply_adjustment")))
            {
                ExecuteExperimentalColorAdjustment();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void GenerateExperimentalPreview()
        {
            if (!CanProcessExperimental()) return;
            
            // Get main color textures from the selected materials
            var refTexture = GetExperimentalReferenceTexture();
            var targetTexture = GetExperimentalTargetTexture();
            
            if (refTexture == null || targetTexture == null) return;
            
            // Use the existing preview generation logic with main color textures
            var originalRefTexture = referenceTexture;
            var originalTargetTexture = this.targetTexture;
            
            // Temporarily set the main color textures for the preview generation
            referenceTexture = refTexture;
            this.targetTexture = targetTexture;
            
            GeneratePreview();
            
            // Restore original textures
            referenceTexture = originalRefTexture;
            this.targetTexture = originalTargetTexture;
        }
        
        private void ApplyColorAdjustmentToMaterial(Texture2D referenceTexture, Texture2D targetTexture, Material targetMaterial)
        {
            // Get the original texture path for saving
            string originalPath = AssetDatabase.GetAssetPath(targetTexture);
            
            if (string.IsNullOrEmpty(originalPath))
            {
                EditorUtility.DisplayDialog("エラー", "対象テクスチャのパスが見つかりません。", "OK");
                return;
            }
            
            // Ask user for save options FIRST before any processing
            var saveOptions = SaveOptionsExtensions.GetDefaultVRChatSettings();
            
            int dialogResult = EditorUtility.DisplayDialogComplex(
                "テクスチャ保存オプション", 
                "調整されたテクスチャをどのように保存しますか？", 
                "新しいファイルとして保存", 
                "元ファイルを上書き",
                "キャンセル"
            );
            
            // Handle dialog result: 0 = save as new, 1 = overwrite, 2 = cancel
            // Note: Unity's DisplayDialogComplex may return different values depending on version
            Debug.Log($"[ApplyColorAdjustmentToMaterial] Dialog result: {dialogResult}");
            
            // Check for cancel first (including any non-zero, non-one values)
            if (dialogResult != 0 && dialogResult != 1)
            {
                Debug.Log($"[ApplyColorAdjustmentToMaterial] User cancelled (result: {dialogResult}) - exiting WITHOUT any processing");
                return; // Exit without ANY processing
            }
            
            if (dialogResult == 0) // Save as new
            {
                Debug.Log("[ApplyColorAdjustmentToMaterial] User chose to save as new file");
                saveOptions.overwriteOriginal = false;
            }
            else if (dialogResult == 1) // Overwrite
            {
                Debug.Log("[ApplyColorAdjustmentToMaterial] User chose to overwrite original file");
                saveOptions.overwriteOriginal = true;
            }
            
            Debug.Log("[ApplyColorAdjustmentToMaterial] Starting color adjustment processing...");
            
            try
            {
                // Use existing color adjustment logic
                var readableTarget = TextureProcessor.MakeTextureReadable(targetTexture);
                var readableReference = TextureProcessor.MakeTextureReadable(referenceTexture);
                
                var adjustedTexture = ColorAdjuster.AdjustColors(
                    readableTarget, 
                    readableReference, 
                    adjustmentIntensity / 100f,
                    preserveLuminance,
                    adjustmentMode
                );
                
                if (adjustedTexture != null)
                {
                    // Save the texture as a file
                    if (TextureExporter.SaveTexture(adjustedTexture, originalPath, saveOptions))
                    {
                        AssetDatabase.Refresh();
                        
                        // Get the saved texture path and load it as an asset
                        string savedPath = GetSavePathForTexture(originalPath, saveOptions);
                        Texture2D savedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(savedPath);
                        
                        if (savedTexture != null)
                        {
                            // Only apply the saved texture to the material if it's a different texture
                            // (i.e., saved as new file, not overwritten)
                            if (!saveOptions.overwriteOriginal)
                            {
                                // Apply the saved texture to the material
                                targetMaterial.SetTexture("_MainTex", savedTexture);
                                
                                // Mark material as dirty to ensure it gets saved
                                EditorUtility.SetDirty(targetMaterial);
                                AssetDatabase.SaveAssets();
                            }
                            
                            EditorUtility.DisplayDialog("成功", "テクスチャの調整が完了しました。", "OK");
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("エラー", "保存されたテクスチャの読み込みに失敗しました。", "OK");
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("エラー", "テクスチャの保存に失敗しました。", "OK");
                    }
                    
                    UnityEngine.Object.DestroyImmediate(adjustedTexture);
                }
                else
                {
                    EditorUtility.DisplayDialog("エラー", "テクスチャの調整に失敗しました。", "OK");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error applying color adjustment: {e.Message}");
                EditorUtility.DisplayDialog("エラー", $"処理中にエラーが発生しました：{e.Message}", "OK");
            }
        }
        
        private bool HasParametersChanged()
        {
            return lastAdjustmentIntensity != adjustmentIntensity ||
                   lastPreserveLuminance != preserveLuminance ||
                   lastPreserveTexture != preserveTexture ||
                   lastAdjustmentMode != adjustmentMode ||
                   lastUseDualColorSelection != useDualColorSelection ||
                   lastSelectedTargetColor != selectedTargetColor ||
                   lastSelectedReferenceColor != selectedReferenceColor ||
                   lastHasSelectedTargetColor != hasSelectedTargetColor ||
                   lastHasSelectedReferenceColor != hasSelectedReferenceColor ||
                   lastColorSelectionRange != colorSelectionRange;
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
            EditorGUI.DrawTextureTransparent(refTextureRect, referenceTexture);
            HandleReferenceTextureInput(refTextureRect);
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Color preview section
            DrawDualColorPreview();
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
                    var readableTexture = TextureProcessor.MakeTextureReadable(targetTexture);
                    hoverTargetColor = readableTexture.GetPixel(pixelX, pixelY);
                    
                    // Change cursor to eyedropper
                    EditorGUIUtility.AddCursorRect(textureRect, MouseCursor.Text);
                    
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
                    var readableTexture = TextureProcessor.MakeTextureReadable(referenceTexture);
                    hoverReferenceColor = readableTexture.GetPixel(pixelX, pixelY);
                    
                    // Change cursor to eyedropper
                    EditorGUIUtility.AddCursorRect(textureRect, MouseCursor.Text);
                    
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
        
        private void DrawDualColorPreview()
        {
            EditorGUILayout.BeginHorizontal();
            
            // Target color preview
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(LocalizationManager.Get("target_color"), EditorStyles.boldLabel);
            
            // Hover color
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationManager.Get("hover_color"), GUILayout.Width(60));
            Rect hoverTargetRect = GUILayoutUtility.GetRect(30, 20, GUILayout.Width(30), GUILayout.Height(20));
            EditorGUI.DrawRect(hoverTargetRect, hoverTargetColor);
            EditorGUILayout.LabelField($"RGB({hoverTargetColor.r:F2}, {hoverTargetColor.g:F2}, {hoverTargetColor.b:F2})", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            
            // Selected color
            if (hasSelectedTargetColor)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(LocalizationManager.Get("selected_color"), GUILayout.Width(60));
                Rect selectedTargetRect = GUILayoutUtility.GetRect(30, 20, GUILayout.Width(30), GUILayout.Height(20));
                EditorGUI.DrawRect(selectedTargetRect, selectedTargetColor);
                EditorGUILayout.LabelField($"RGB({selectedTargetColor.r:F2}, {selectedTargetColor.g:F2}, {selectedTargetColor.b:F2})", EditorStyles.miniLabel);
                if (GUILayout.Button("✕", GUILayout.Width(20), GUILayout.Height(20)))
                {
                    hasSelectedTargetColor = false;
                    Repaint();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(20);
            
            // Reference color preview
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(LocalizationManager.Get("reference_color"), EditorStyles.boldLabel);
            
            // Hover color
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationManager.Get("hover_color"), GUILayout.Width(60));
            Rect hoverRefRect = GUILayoutUtility.GetRect(30, 20, GUILayout.Width(30), GUILayout.Height(20));
            EditorGUI.DrawRect(hoverRefRect, hoverReferenceColor);
            EditorGUILayout.LabelField($"RGB({hoverReferenceColor.r:F2}, {hoverReferenceColor.g:F2}, {hoverReferenceColor.b:F2})", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            
            // Selected color
            if (hasSelectedReferenceColor)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(LocalizationManager.Get("selected_color"), GUILayout.Width(60));
                Rect selectedRefRect = GUILayoutUtility.GetRect(30, 20, GUILayout.Width(30), GUILayout.Height(20));
                EditorGUI.DrawRect(selectedRefRect, selectedReferenceColor);
                EditorGUILayout.LabelField($"RGB({selectedReferenceColor.r:F2}, {selectedReferenceColor.g:F2}, {selectedReferenceColor.b:F2})", EditorStyles.miniLabel);
                if (GUILayout.Button("✕", GUILayout.Width(20), GUILayout.Height(20)))
                {
                    hasSelectedReferenceColor = false;
                    Repaint();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            // Color selection range control
            if (hasSelectedTargetColor || hasSelectedReferenceColor)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField(LocalizationManager.Get("color_selection_range"), EditorStyles.boldLabel);
                colorSelectionRange = EditorGUILayout.Slider(LocalizationManager.Get("selection_range"), colorSelectionRange, 0.1f, 1.0f);
                EditorGUILayout.HelpBox(LocalizationManager.Get("selection_range_help"), MessageType.Info);
            }
            
            // Status indicator
            if (hasSelectedTargetColor && hasSelectedReferenceColor)
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox(LocalizationManager.Get("colors_selected_ready"), MessageType.Info);
                
                // Show selected colors for debugging
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Target: RGB({selectedTargetColor.r:F2}, {selectedTargetColor.g:F2}, {selectedTargetColor.b:F2})", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Reference: RGB({selectedReferenceColor.r:F2}, {selectedReferenceColor.g:F2}, {selectedReferenceColor.b:F2})", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                
                // Show Lab values for better understanding
                Vector3 targetLab = ColorSpaceConverter.RGBtoLAB(selectedTargetColor);
                Vector3 referenceLab = ColorSpaceConverter.RGBtoLAB(selectedReferenceColor);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Target Lab: L={targetLab.x:F1}, A={targetLab.y:F1}, B={targetLab.z:F1}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Reference Lab: L={referenceLab.x:F1}, A={referenceLab.y:F1}, B={referenceLab.z:F1}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            else if (hasSelectedTargetColor || hasSelectedReferenceColor)
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox(LocalizationManager.Get("select_both_colors"), MessageType.Warning);
            }
        }
        
        private void GenerateSingleTexturePreview()
        {
            if (singleTexture == null) return;
            
            try
            {
                var readableTexture = TextureProcessor.MakeTextureReadable(singleTexture);
                
                // Create preview copy
                singleTexturePreview = DuplicateTexture(readableTexture);
                
                if (singleTexturePreview != null)
                {
                    // Apply adjustments
                    Color[] pixels = TextureUtils.GetPixelsSafe(singleTexturePreview);
                    if (pixels != null)
                    {
                        Color[] adjustedPixels = ColorSpaceConverter.ApplyGammaSaturationBrightnessToArray(
                            pixels, singleGammaAdjustment, singleSaturationAdjustment, singleBrightnessAdjustment);
                        
                        TextureUtils.SetPixelsSafe(singleTexturePreview, adjustedPixels);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error generating single texture preview: {e.Message}");
            }
        }
        
        private void ApplySingleTextureAdjustment()
        {
            if (singleTexture == null || singleTexturePreview == null) return;
            
            string originalPath = AssetDatabase.GetAssetPath(singleTexture);
            var saveOptions = SaveOptionsExtensions.GetDefaultVRChatSettings();
            
            // Use DisplayDialogComplex to include cancel option
            int dialogResult = EditorUtility.DisplayDialogComplex(
                LocalizationManager.Get("apply_adjustment_title"), 
                LocalizationManager.Get("apply_adjustment_message"), 
                LocalizationManager.Get("save_as_new"), 
                LocalizationManager.Get("overwrite_original"),
                LocalizationManager.Get("cancel")
            );
            
            // Handle dialog result: 0 = save as new, 1 = overwrite, 2 = cancel
            // Note: Unity's DisplayDialogComplex may return different values depending on version
            Debug.Log($"[ApplySingleTextureAdjustment] Dialog result: {dialogResult}");
            
            // Check for cancel first (including any non-zero, non-one values)
            if (dialogResult != 0 && dialogResult != 1)
            {
                Debug.Log($"[ApplySingleTextureAdjustment] User cancelled (result: {dialogResult}) - exiting");
                return; // Exit without processing
            }
            
            if (dialogResult == 0) // Save as new
            {
                Debug.Log("[ApplySingleTextureAdjustment] User chose to save as new file");
                saveOptions.overwriteOriginal = false;
            }
            else if (dialogResult == 1) // Overwrite
            {
                Debug.Log("[ApplySingleTextureAdjustment] User chose to overwrite original file");
                saveOptions.overwriteOriginal = true;
            }
            
            try
            {
                var readableTexture = TextureProcessor.MakeTextureReadable(singleTexture);
                var result = DuplicateTexture(readableTexture);
                
                if (result != null)
                {
                    // Apply adjustments to full resolution
                    Color[] pixels = TextureUtils.GetPixelsSafe(result);
                    if (pixels != null)
                    {
                        Color[] adjustedPixels = ColorSpaceConverter.ApplyGammaSaturationBrightnessToArray(
                            pixels, singleGammaAdjustment, singleSaturationAdjustment, singleBrightnessAdjustment);
                        
                        TextureUtils.SetPixelsSafe(result, adjustedPixels);
                        
                        if (TextureExporter.SaveTexture(result, originalPath, saveOptions))
                        {
                            EditorUtility.DisplayDialog(LocalizationManager.Get("success_title"), LocalizationManager.Get("success_message"), LocalizationManager.Get("ok"));
                        }
                        else
                        {
                            EditorUtility.DisplayDialog(LocalizationManager.Get("error_title"), LocalizationManager.Get("error_save_message"), LocalizationManager.Get("ok"));
                        }
                    }
                    
                    UnityEngine.Object.DestroyImmediate(result);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error applying single texture adjustment: {e.Message}");
                EditorUtility.DisplayDialog(LocalizationManager.Get("error_title"), LocalizationManager.Get("error_save_message"), LocalizationManager.Get("ok"));
            }
        }
        
        private void DrawSingleTexturePreview()
        {
            if (singleTexturePreview == null) return;
            
            float aspectRatio = (float)singleTexturePreview.width / singleTexturePreview.height;
            float previewHeight = 200f;
            float previewWidth = previewHeight * aspectRatio;
            
            GUILayout.Space(10);
            EditorGUILayout.LabelField(LocalizationManager.Get("preview"), EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            // Original
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(LocalizationManager.Get("original"), EditorStyles.centeredGreyMiniLabel);
            GUILayout.Label(singleTexture, GUILayout.Width(previewWidth/2), GUILayout.Height(previewHeight/2));
            EditorGUILayout.EndVertical();
            
            // Adjusted
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(LocalizationManager.Get("adjusted"), EditorStyles.centeredGreyMiniLabel);
            GUILayout.Label(singleTexturePreview, GUILayout.Width(previewWidth/2), GUILayout.Height(previewHeight/2));
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void CheckForAutoPreview()
        {
            if (referenceTexture != null && targetTexture != null && previewTexture == null)
            {
                // Auto-generate preview when both textures are selected
                EditorApplication.delayCall += () => {
                    if (CanProcess())
                    {
                        GeneratePreview();
                    }
                };
            }
        }
        
        private void CheckForExperimentalAutoPreview()
        {
            if (GetExperimentalReferenceTexture() != null && GetExperimentalTargetTexture() != null && previewTexture == null)
            {
                // Auto-generate preview when both main color textures are available
                EditorApplication.delayCall += () => {
                    if (CanProcessExperimental())
                    {
                        GenerateExperimentalPreview();
                    }
                };
            }
        }
        
        private void DrawShaderSettingsTab()
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
        
    }
    
    [Serializable]
    public class ColorAdjustmentPreset
    {
        public string name;
        public float intensity;
        public bool preserveLuminance;
        public ColorAdjustmentMode mode;
        
        public ColorAdjustmentPreset(string name, float intensity, bool preserveLuminance, ColorAdjustmentMode mode)
        {
            this.name = name;
            this.intensity = intensity;
            this.preserveLuminance = preserveLuminance;
            this.mode = mode;
        }
    }
    
    public enum ColorAdjustmentMode
    {
        LabHistogramMatching,
        HueShift,
        ColorTransfer,
        AdaptiveAdjustment
    }
}