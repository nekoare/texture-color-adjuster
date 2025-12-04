using System;
using TexColorAdjusterNamespace;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using TexColAdjuster.Editor;
using ColorAdjustmentMode = TexColAdjuster.Runtime.ColorAdjustmentMode;

namespace TexColAdjuster
{
    public class TexColAdjusterWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private int activeTab = 0;
        private readonly string[] tabs = { "Basic", "Direct", "Color Change", "Shader Settings" };
        
        // Texture references
        private Texture2D referenceTexture;
        private Texture2D targetTexture;
        private Texture2D previewTexture;
        private Texture2D originalTexture;
        
        // Smart color matching configuration
        private ColorTransformConfig transformConfig = new ColorTransformConfig();
        private ColorPixel selectedFromColor = new ColorPixel(255, 255, 255);
        private ColorPixel selectedToColor = new ColorPixel(255, 255, 255);
        private bool hasSelectedFromColor = false;
        private bool hasSelectedToColor = false;
        
        // UI state
        private bool showPreview = true;
        private bool realTimePreview = true;
        private bool directTabAutoPreviewEnabled = true;
        private bool directTabPreviewPending = false;
        private bool directTabPreviewInFlight = false;
        private bool directTabHasQueuedParameters = false;
        private double directTabNextPreviewTime = 0d;
        private DirectPreviewParameterState directTabQueuedParameterState;
        private const double DirectTabPreviewDebounceSeconds = 0.2d;
        private bool isProcessing = false;
        private float processingProgress = 0f;
        
        // Selection state
        private System.Collections.BitArray selectionMask = null;
        private SelectionMode currentSelectionMode = SelectionMode.None;
        
        private enum SelectionMode
        {
            None,
            FromColor,
            ToColor,
            AreaSelection
        }
        
        // Interactive color selection
        private Color hoverColor = Color.white;
        private Vector2Int lastClickPosition = Vector2Int.zero;
        private MeshInfo currentMeshInfo = null;
        private bool showMeshInfo = true;
        
        // Legacy dual color selection variables (for backward compatibility)
        private bool useDualColorSelection = false;
        private Color selectedTargetColor = Color.white;
        private Color selectedReferenceColor = Color.white;
        private Color hoverTargetColor = Color.white;
        private Color hoverReferenceColor = Color.white;
        private bool hasSelectedTargetColor = false;
        private bool hasSelectedReferenceColor = false;
        private Vector2 targetTextureScrollPosition;
        private Vector2 referenceTextureScrollPosition;
        private float colorSelectionRange = 1.0f;
        private Texture2D cachedTargetReadableForPicking;
        private Texture2D cachedTargetReadableSource;
        private Texture2D cachedReferenceReadableForPicking;
        private Texture2D cachedReferenceReadableSource;
        
        // Legacy single texture variables
        private Texture2D singleTexture;
        private Texture2D singleTexturePreview;
        private float singleGammaAdjustment = 1.0f;
        private float singleSaturationAdjustment = 1.0f;
        private float singleBrightnessAdjustment = 1.0f;
        
        // Single texture color adjustment controls
        private float hueShift = 0f;
        private float saturationMultiplier = 1f;
        private float brightnessOffset = 0f;
        private float contrastMultiplier = 1f;
        
        // Legacy adjustment parameters (for Advanced tab)
        private float adjustmentIntensity = 100f;
        private bool preserveLuminance = false;
        private bool preserveTexture = true;
        private ColorAdjustmentMode adjustmentMode = ColorAdjustmentMode.LabHistogramMatching;
        
        // Basic tab processing parameters
        private float intensity = 100f;
        private ColorAdjustmentMode colorAdjustmentMode = ColorAdjustmentMode.LabHistogramMatching;
        
        // High-precision mode settings
        private bool useHighPrecisionMode = false;
        private bool lastDirectTabHighPrecisionState = false;
        private HighPrecisionProcessor.HighPrecisionConfig highPrecisionConfig = new HighPrecisionProcessor.HighPrecisionConfig();
        private Texture2D highPrecisionPreviewTexture = null;
        private Texture2D uvMaskPreviewTexture = null;
        private string uvUsageStats = "";
        
        // NDMF integration
        private bool useNDMFDirectMode = true;
        
        // Balance mode settings
        private bool useBalanceMode = true;
        
        // Parameter change tracking for Smart Color Match
        private ColorTransformConfig lastTransformConfig;
        private ColorPixel lastFromColor;
        private ColorPixel lastToColor;
        private bool lastHasFromColor = false;
        private bool lastHasToColor = false;
        
        // Legacy parameter tracking (for Advanced tab)
        private float lastAdjustmentIntensity = -1f;
        private bool lastPreserveLuminance = false;
        private bool lastPreserveTexture = true;
        private ColorAdjustmentMode lastAdjustmentMode = ColorAdjustmentMode.LabHistogramMatching;
        private bool lastUseDualColorSelection = false;
        private Color lastSelectedTargetColor = Color.white;
        private Color lastSelectedReferenceColor = Color.white;
        private bool lastHasSelectedTargetColor = false;
        private bool lastHasSelectedReferenceColor = false;
        private float lastColorSelectionRange = -1f;
        private bool lastUseHighPrecisionModeForPreview = false;
        
        // Presets
        private List<ColorAdjustmentPreset> presets = new List<ColorAdjustmentPreset>();
        
        // Liltoon preset variables (simplified for 2-material workflow)
        private Material sourceLiltoonMaterial;
        private List<Material> targetLiltoonMaterials = new List<Material>();
        
        // Material Unify Tool integration (old variables removed to avoid conflicts)
        private bool includeInactiveObjects = false;
        private int shaderTransferTab = 0; // 0: Simple, 1: Advanced
        
        private MaterialUnifyToolMethods.TransferCategories selectedCategories = MaterialUnifyToolMethods.TransferCategories.None;
        
        // Texture transfer flags
        private bool transferReflectionCubeTex = false;
        private bool transferMatCapTex = false;
        private bool transferMatCapBumpMask = false;
        private bool transferMatCap2ndTex = false;
        private bool transferMatCap2ndBumpMask = false;
        
        // Shader transfer specific variables (renamed to avoid conflicts)
        private GameObject shaderTransferSourceGameObject;
        private List<GameObject> shaderTransferTargetGameObjects = new List<GameObject>();
        private List<Material> shaderTransferSourceAvailableMaterials = new List<Material>();
        private int shaderTransferSelectedSourceMaterialIndex = -1;
        private List<Material> shaderTransferTargetAvailableMaterials = new List<Material>();
        private List<bool> shaderTransferTargetSelectedMaterials = new List<bool>();
        private Dictionary<Material, List<GameObject>> shaderTransferSourceMaterialToGameObjects = new Dictionary<Material, List<GameObject>>();
        private Dictionary<Material, List<GameObject>> shaderTransferTargetMaterialToGameObjects = new Dictionary<Material, List<GameObject>>();
        private Dictionary<Material, bool> shaderTransferSourceMaterialFoldouts = new Dictionary<Material, bool>();
        private Dictionary<Material, bool> shaderTransferTargetMaterialFoldouts = new Dictionary<Material, bool>();
        
        // Experimental tab variables
        private GameObject referenceGameObject;
        private GameObject targetGameObject;
        private Component referenceComponent;
        private Component targetComponent;
        private Material selectedReferenceMaterial;
        private Material selectedTargetMaterial;
        
        // Material transfer option for direct tab
        private bool enableMaterialTransfer = false;
        private int materialTransferDirection = 0; // 0: Reference → Target, 1: Target → Reference
        
        
        // Color Adjust tab - Balance Mode variables (ColorChanger style)
        private GameObject balanceGameObject;
        private Component balanceRendererComponent;
        private Material balanceSelectedMaterial;
        private Color previousColor = Color.white; // Source color in ColorChanger
        private Color newColor = Color.white; // Target color in ColorChanger
        private bool balanceModeEnabled = true;
        private bool transparentModeEnabled = false;
        private enum BalanceModeVersion { V1_Distance, V2_Radius, V3_Gradient }
        private BalanceModeVersion balanceModeVersion = BalanceModeVersion.V1_Distance;
        
        // Texture preview for color picking
        private Vector2 texturePreviewScrollPosition;
        private bool showTexturePreview = true;
        
        // Balance Mode V1 settings
        private float v1Weight = 1.0f;
        private float v1MinimumValue = 0.0f;
        
        // Balance Mode V2 settings  
        private float v2Weight = 1.0f;
        private float v2Radius = 0.0f;
        private float v2MinimumValue = 0.0f;
        private bool v2IncludeOutside = false;
        
        // Balance Mode V3 settings
        private Color v3GradientColor = Color.white;
        private float v3GradientStart = 0f;
        private float v3GradientEnd = 100f;
        
        // Advanced Color Configuration
        private bool enableBrightness = false;
        private float brightness = 1.0f;
        private bool enableContrast = false;
        private float contrast = 1.0f;
        private bool enableGamma = false;
        private float gamma = 1.0f;
        private bool enableExposure = false;
        private float exposure = 0.0f;
        private bool enableTransparency = false;
        private float transparency = 0.0f;

        // Cached GUI styles
        private GUIStyle largeBoldLabelStyle;
        private GUIStyle largeToggleLabelStyle;

        private static readonly Color DropLabelMissingColor = new Color(250f / 255f, 0f, 0f);
        private static readonly Color DropLabelReadyColor = new Color(0f, 220f / 255f, 0f);
        
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
            useNDMFDirectMode = NDMFIntegrationHelper.GetNDMFMode("Direct");
        }
        
        private void OnDisable()
        {
            CancelDirectTabAutoPreview();
            DisposeReadableTextureCaches();
            ClearUVMaskPreview();
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
                if (activeTab == 0 && CanProcessSmartColorMatch() && HasSmartColorMatchParametersChanged())
                {
                    GenerateSmartColorMatchPreview();
                    UpdateSmartColorMatchParameterCache();
                }
                else if (activeTab == 1)
                {
                    HandleDirectTabAutoPreview();
                }
            }
        }
        
        private void HandleDirectTabAutoPreview()
        {
            if (!directTabAutoPreviewEnabled)
            {
                CancelDirectTabAutoPreview(false);
                return;
            }

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

            var currentState = CaptureCurrentDirectPreviewState();

            if (HasParametersChanged())
            {
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
                directTabNextPreviewTime = now + DirectTabPreviewDebounceSeconds;
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
                useHighPrecisionMode
            );
        }

        private void HandleDirectMaterialSelectionChanged()
        {
            ClearPreview();

            if (directTabAutoPreviewEnabled && realTimePreview && CanProcessExperimental())
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
            
            GUILayout.Space(10);
        }
        
        private void DrawTabs()
        {
            string[] localizedTabs = new string[]
            {
                LocalizationManager.Get("tab_basic"),
                LocalizationManager.Get("tab_direct"),
                LocalizationManager.Get("tab_shader_settings")
            };
            
            activeTab = Mathf.Clamp(activeTab, 0, localizedTabs.Length - 1);
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
                    DrawShaderSettingsTab();
                    break;
            }
        }
        
        // Main Smart Color Match tab
        private void DrawSmartColorMatchTab()
        {
            // Simple workflow description
            EditorGUILayout.HelpBox("🎨 TexColAdjuster Smart Match:\n1. Select your texture\n2. Pick source and target colors\n3. Preview and apply intelligent color transformation!", MessageType.Info);
            
            GUILayout.Space(10);
            
            // Texture selection
            EditorGUILayout.LabelField("🖼️ Texture Selection", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target Texture:", GUILayout.Width(100));
            var newTargetTexture = (Texture2D)EditorGUILayout.ObjectField(targetTexture, typeof(Texture2D), false);
            if (newTargetTexture != targetTexture)
            {
                targetTexture = newTargetTexture;
                ClearPreview();
                ResetColorSelection();
            }
            EditorGUILayout.EndHorizontal();
            
            if (targetTexture == null)
            {
                EditorGUILayout.HelpBox("📁 Please select a texture to start color changing.", MessageType.Info);
                return;
            }
            
            GUILayout.Space(15);
            
            // Color selection interface
            DrawColorSelectionInterface();
            
            GUILayout.Space(10);
            
            // Settings panel
            DrawSimpleSettingsPanel();
            
            GUILayout.Space(10);
            
            // Action buttons
            DrawColorChangerActionButtons();
            
            GUILayout.Space(10);
            
            // Preview section
            if (showPreview && previewTexture != null)
            {
                DrawTexColAdjusterPreview();
            }
        }
        
        private void DrawInputsResetControl()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(LocalizationManager.Get("reset_inputs"), GUILayout.Width(120)))
            {
                ResetInputsToInitialState();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }

        private void DrawBasicTab()
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
        
        private void DrawColorAdjustTab()
        {
            EditorGUILayout.LabelField("⚖️ Color Adjust - ColorChanger Style", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Color balance adjustment using ColorChanger's algorithm. Select Previous Color from texture preview, set New Color, and apply balance adjustment.", MessageType.Info);
            
            GUILayout.Space(10);
            
            // GameObject and Material selection
            DrawMaterialSelectionForColorAdjust();
            
            if (balanceSelectedMaterial == null)
            {
                EditorGUILayout.HelpBox("Please select a GameObject with a Material containing a texture.", MessageType.Info);
                return;
            }
            
            var materialTexture = GetMainTexture(balanceSelectedMaterial);
            if (materialTexture == null)
            {
                EditorGUILayout.HelpBox("Selected material does not have a main texture.", MessageType.Warning);
                return;
            }
            
            GUILayout.Space(10);
            
            // Color selection section
            DrawColorSelectionSection();
            
            GUILayout.Space(10);
            
            // Texture preview for color picking
            if (showTexturePreview)
            {
                DrawTexturePreviewForColorPicking(materialTexture);
            }
            
            GUILayout.Space(10);
            
            // Balance settings (simplified)
            DrawSimplifiedBalanceSettings();
            
            GUILayout.Space(10);
            
            // Advanced color settings
            DrawAdvancedColorSettings();
            
            GUILayout.Space(10);
            
            // Preview and action buttons
            DrawBalanceActions();
            
            // Preview display
            if (showPreview && previewTexture != null)
            {
                DrawPreview();
            }
        }
        
        // Advanced tab (replaces old Color Adjust tab)
        private void DrawAdvancedTab()
        {
            EditorGUILayout.LabelField("🔬 Advanced Color Processing", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox("Advanced mode using traditional histogram matching and color transfer algorithms.", MessageType.Info);
            
            GUILayout.Space(10);
            
            // Texture selection
            EditorGUILayout.LabelField("📁 Texture Selection", EditorStyles.boldLabel);
            
            // Reference texture
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Reference:", GUILayout.Width(80));
            var newReferenceTexture = (Texture2D)EditorGUILayout.ObjectField(referenceTexture, typeof(Texture2D), false);
            if (newReferenceTexture != referenceTexture)
            {
                referenceTexture = newReferenceTexture;
                ClearPreview();
            }
            EditorGUILayout.EndHorizontal();
            
            // Target texture  
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target:", GUILayout.Width(80));
            var newTargetTexture = (Texture2D)EditorGUILayout.ObjectField(targetTexture, typeof(Texture2D), false);
            if (newTargetTexture != targetTexture)
            {
                targetTexture = newTargetTexture;
                ClearPreview();
            }
            EditorGUILayout.EndHorizontal();
            
            if (referenceTexture == null || targetTexture == null)
            {
                EditorGUILayout.HelpBox("Please select both reference and target textures.", MessageType.Info);
                return;
            }
            
            GUILayout.Space(10);
            
            GUILayout.Space(10);
            
            // Adjustment parameters
            EditorGUILayout.LabelField(LocalizationManager.Get("adjustment_parameters"), EditorStyles.boldLabel);
            
            GUILayout.Space(10);
            
            // Advanced adjustment parameters
            EditorGUILayout.LabelField("⚙️ Adjustment Parameters", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical("box");
            
            // Adjustment intensity
            adjustmentIntensity = EditorGUILayout.Slider("Intensity:", adjustmentIntensity, 0f, 100f);
            
            // Options
            preserveLuminance = EditorGUILayout.Toggle("Preserve Luminance:", preserveLuminance);
            preserveTexture = EditorGUILayout.Toggle("Preserve Texture:", preserveTexture);
            
            // Adjustment mode
            adjustmentMode = (ColorAdjustmentMode)EditorGUILayout.EnumPopup("Processing Mode:", adjustmentMode);
            
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);
            
            GUILayout.Space(10);
            
            // Action buttons
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = CanProcess();
            if (GUILayout.Button("🔍 Generate Preview", GUILayout.Height(30)))
            {
                if (useHighPrecisionMode)
                {
                    GenerateHighPrecisionPreview();
                }
                else
                {
                    GeneratePreview();
                }
            }
            
            GUI.enabled = previewTexture != null;
            if (GUILayout.Button("✨ Apply Changes", GUILayout.Height(30)))
            {
                if (useHighPrecisionMode)
                {
                    ApplyHighPrecisionAdjustment();
                }
                else
                {
                    ApplyAdjustment();
                }
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Preview display
            if (previewTexture != null)
            {
                DrawPreview();
            }
            
            // High-precision preview display
            if (useHighPrecisionMode && highPrecisionPreviewTexture != null)
            {
                DrawHighPrecisionReferencePreview();
            }
        }
        
        private void DrawDirectTab()
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
            preserveTexture = EditorGUILayout.Toggle(LocalizationManager.Get("preserve_texture"), preserveTexture);
            
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
                enableMaterialTransfer = EditorGUILayout.Toggle("見え方も転送(マテリアル設定の転送)", enableMaterialTransfer);
                
                if (enableMaterialTransfer)
                {
                    EditorGUILayout.HelpBox("💡 色調整と同時にliltoonのマテリアル設定（描画効果等）も転送されます。", MessageType.Info);
                    
                    GUILayout.Space(5);
                    
                    // Transfer direction selection with visual indicators
                    EditorGUILayout.LabelField("転送方向:", EditorStyles.boldLabel);
                    
                    EditorGUILayout.BeginVertical("box");
                    
                    // Direction 0: Reference → Target
                    EditorGUILayout.BeginHorizontal();
                    bool direction0Selected = materialTransferDirection == 0;
                    if (direction0Selected) GUI.color = Color.green;
                    
                    bool newDirection0 = EditorGUILayout.Toggle(direction0Selected, GUILayout.Width(20));
                    if (newDirection0 && !direction0Selected)
                        materialTransferDirection = 0;
                    
                    GUI.color = Color.white;
                    EditorGUILayout.LabelField($"参照用 ({(selectedReferenceMaterial != null ? selectedReferenceMaterial.name : "未選択")}) ", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField("→", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(20));
                    EditorGUILayout.LabelField($" 変更対象 ({(selectedTargetMaterial != null ? selectedTargetMaterial.name : "未選択")})", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                    
                    GUILayout.Space(2);
                    
                    // Direction 1: Target → Reference
                    EditorGUILayout.BeginHorizontal();
                    bool direction1Selected = materialTransferDirection == 1;
                    if (direction1Selected) GUI.color = Color.green;
                    
                    bool newDirection1 = EditorGUILayout.Toggle(direction1Selected, GUILayout.Width(20));
                    if (newDirection1 && !direction1Selected)
                        materialTransferDirection = 1;
                    
                    GUI.color = Color.white;
                    EditorGUILayout.LabelField($"変更対象 ({(selectedTargetMaterial != null ? selectedTargetMaterial.name : "未選択")}) ", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField("→", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(20));
                    EditorGUILayout.LabelField($" 参照用 ({(selectedReferenceMaterial != null ? selectedReferenceMaterial.name : "未選択")})", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.EndVertical();
                    
                    GUILayout.Space(5);
                    
                    // Show material compatibility status for the selected direction
                    Material sourceMaterial = materialTransferDirection == 0 ? selectedReferenceMaterial : selectedTargetMaterial;
                    Material targetMaterial = materialTransferDirection == 0 ? selectedTargetMaterial : selectedReferenceMaterial;
                    
                    bool sourceLiltoon = IsLiltoonMaterial(sourceMaterial);
                    bool targetLiltoon = IsLiltoonMaterial(targetMaterial);
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("転送元:", GUILayout.Width(50));
                    GUI.color = sourceLiltoon ? Color.green : Color.red;
                    EditorGUILayout.LabelField(sourceMaterial != null ? sourceMaterial.name : "未選択", EditorStyles.boldLabel);
                    GUI.color = Color.white;
                    EditorGUILayout.LabelField(sourceLiltoon ? "✓ liltoon" : "⚠ 非liltoon", GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("転送先:", GUILayout.Width(50));
                    GUI.color = targetLiltoon ? Color.green : Color.red;
                    EditorGUILayout.LabelField(targetMaterial != null ? targetMaterial.name : "未選択", EditorStyles.boldLabel);
                    GUI.color = Color.white;
                    EditorGUILayout.LabelField(targetLiltoon ? "✓ liltoon" : "⚠ 非liltoon", GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();
                    
                    if (!sourceLiltoon || !targetLiltoon)
                    {
                        EditorGUILayout.HelpBox("⚠ 両方のマテリアルがliltoonである必要があります。", MessageType.Warning);
                    }
                }
                
                GUILayout.Space(10);
            }
            
            DrawNDMFOptionsForDirectTab();
            
            // Preview controls
            EditorGUILayout.LabelField(LocalizationManager.Get("preview"), EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            showPreview = EditorGUILayout.Toggle(LocalizationManager.Get("show_preview"), showPreview);
            bool autoPreviewToggle = EditorGUILayout.Toggle(LocalizationManager.Get("direct_auto_preview"), directTabAutoPreviewEnabled);
            if (autoPreviewToggle != directTabAutoPreviewEnabled)
            {
                directTabAutoPreviewEnabled = autoPreviewToggle;
                if (!directTabAutoPreviewEnabled)
                {
                    CancelDirectTabAutoPreview();
                }
                else if (realTimePreview && CanProcessExperimental())
                {
                    RequestDirectTabAutoPreview(CaptureCurrentDirectPreviewState());
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            bool previousRealTimePreview = realTimePreview;
            realTimePreview = EditorGUILayout.Toggle(LocalizationManager.Get("realtime_preview"), realTimePreview);
            if (previousRealTimePreview != realTimePreview)
            {
                if (!realTimePreview)
                {
                    CancelDirectTabAutoPreview();
                }
                else if (directTabAutoPreviewEnabled && CanProcessExperimental())
                {
                    RequestDirectTabAutoPreview(CaptureCurrentDirectPreviewState());
                }
            }
            showMeshInfo = EditorGUILayout.Toggle("メッシュ情報を表示", showMeshInfo);
            EditorGUILayout.EndHorizontal();
            
            if (realTimePreview && directTabAutoPreviewEnabled)
            {
                EditorGUILayout.HelpBox("💡 動作が重い場合はリアルタイムプレビューをオフにしてください", MessageType.Info);
            }
            else if (CanProcessExperimental() && HasParametersChanged())
            {
                EditorGUILayout.HelpBox(LocalizationManager.Get("direct_manual_preview_hint"), MessageType.Info);
            }
            
            if (showPreview && GetExperimentalTargetTexture() != null)
            {
                DrawPreview();
            }
            
            GUILayout.Space(10);
            
            // Action buttons
            DrawDirectTabActionButtons();
            
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
                0f,
                1f,
                1f,
                1f,
                highPrecisionActive,
                highPrecisionActive ? highPrecisionConfig.referenceGameObject : null,
                highPrecisionConfig.materialIndex,
                highPrecisionConfig.uvChannel,
                highPrecisionConfig.dominantColorCount,
                highPrecisionConfig.useWeightedSampling);

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
        }
        
        private void CheckForAutoPreview()
        {
            if (referenceTexture != null && targetTexture != null && previewTexture == null)
            {
                // Auto-generate preview when both textures are selected
                EditorApplication.delayCall += () => {
                    if (CanProcess())
                    {
                        if (useHighPrecisionMode)
                        {
                            GenerateHighPrecisionPreview();
                        }
                        else
                        {
                            GeneratePreview();
                        }
                    }
                };
            }
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
        
        private void DrawNDMFOptionsForDirectTab()
        {
            EditorGUILayout.LabelField("NDMF", GetLargeBoldLabelStyle());
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
                includeInactiveObjects = EditorGUILayout.Toggle("非アクティブも対象", includeInactiveObjects);
            }
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSingleTextureColorControls()
        {
            EditorGUILayout.LabelField("Color Adjustment Controls", EditorStyles.boldLabel);
            
            // Hue adjustment
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Hue Shift:", GUILayout.Width(150));
            hueShift = EditorGUILayout.Slider(hueShift, -180f, 180f);
            EditorGUILayout.EndHorizontal();
            
            // Saturation adjustment
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Saturation:", GUILayout.Width(150));
            saturationMultiplier = EditorGUILayout.Slider(saturationMultiplier, 0f, 2f);
            EditorGUILayout.EndHorizontal();
            
            // Brightness adjustment
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Brightness:", GUILayout.Width(150));
            brightnessOffset = EditorGUILayout.Slider(brightnessOffset, -1f, 1f);
            EditorGUILayout.EndHorizontal();
            
            // Contrast adjustment
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Contrast:", GUILayout.Width(150));
            contrastMultiplier = EditorGUILayout.Slider(contrastMultiplier, 0f, 2f);
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawSingleTextureActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = (singleTexture != null);
            if (GUILayout.Button("Generate Preview"))
            {
                GenerateSingleTexturePreview();
            }
            
            if (GUILayout.Button("Apply Adjustment"))
            {
                ApplySingleTextureAdjustment();
            }
            
            if (GUILayout.Button("Reset"))
            {
                ResetSingleTextureAdjustments();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void ResetSingleTextureAdjustments()
        {
            hueShift = 0f;
            saturationMultiplier = 1f;
            brightnessOffset = 0f;
            contrastMultiplier = 1f;
            
            if (singleTexturePreview != null)
            {
                UnityEngine.Object.DestroyImmediate(singleTexturePreview, true);
                singleTexturePreview = null;
            }
        }

        private void ResetInputsToInitialState()
        {
            // Reset user-adjustable inputs back to the initial window state.
            ClearPreview();
            DisposeReadableTextureCaches();
            ClearUVMaskPreview();
            ResetSingleTextureAdjustments();

            scrollPosition = Vector2.zero;
            referenceTexture = null;
            targetTexture = null;
            transformConfig = new ColorTransformConfig();
            selectedFromColor = new ColorPixel(255, 255, 255);
            selectedToColor = new ColorPixel(255, 255, 255);
            hasSelectedFromColor = false;
            hasSelectedToColor = false;

            showPreview = true;
            realTimePreview = true;
            directTabAutoPreviewEnabled = true;
            directTabPreviewPending = false;
            directTabPreviewInFlight = false;
            directTabHasQueuedParameters = false;
            directTabNextPreviewTime = 0d;
            directTabQueuedParameterState = default;
            isProcessing = false;
            processingProgress = 0f;

            selectionMask = null;
            currentSelectionMode = SelectionMode.None;
            hoverColor = Color.white;
            lastClickPosition = Vector2Int.zero;
            currentMeshInfo = null;
            showMeshInfo = true;

            useDualColorSelection = false;
            selectedTargetColor = Color.white;
            selectedReferenceColor = Color.white;
            hoverTargetColor = Color.white;
            hoverReferenceColor = Color.white;
            hasSelectedTargetColor = false;
            hasSelectedReferenceColor = false;
            targetTextureScrollPosition = Vector2.zero;
            referenceTextureScrollPosition = Vector2.zero;
            colorSelectionRange = 1.0f;

            singleTexture = null;
            singleGammaAdjustment = 1.0f;
            singleSaturationAdjustment = 1.0f;
            singleBrightnessAdjustment = 1.0f;

            adjustmentIntensity = 100f;
            preserveLuminance = false;
            preserveTexture = true;
            adjustmentMode = ColorAdjustmentMode.LabHistogramMatching;

            intensity = 100f;
            colorAdjustmentMode = ColorAdjustmentMode.LabHistogramMatching;

            useHighPrecisionMode = false;
            lastDirectTabHighPrecisionState = false;
            highPrecisionConfig = new HighPrecisionProcessor.HighPrecisionConfig();
            uvUsageStats = "";

            useNDMFDirectMode = NDMFIntegrationHelper.GetNDMFMode("Direct");

            useBalanceMode = true;

            lastTransformConfig = null;
            lastFromColor = new ColorPixel(255, 255, 255);
            lastToColor = new ColorPixel(255, 255, 255);
            lastHasFromColor = false;
            lastHasToColor = false;

            lastAdjustmentIntensity = -1f;
            lastPreserveLuminance = false;
            lastPreserveTexture = true;
            lastAdjustmentMode = ColorAdjustmentMode.LabHistogramMatching;
            lastUseDualColorSelection = false;
            lastSelectedTargetColor = Color.white;
            lastSelectedReferenceColor = Color.white;
            lastHasSelectedTargetColor = false;
            lastHasSelectedReferenceColor = false;
            lastColorSelectionRange = -1f;
            lastUseHighPrecisionModeForPreview = false;

            sourceLiltoonMaterial = null;
            targetLiltoonMaterials?.Clear();

            includeInactiveObjects = false;
            shaderTransferTab = 0;
            selectedCategories = MaterialUnifyToolMethods.TransferCategories.None;

            transferReflectionCubeTex = false;
            transferMatCapTex = false;
            transferMatCapBumpMask = false;
            transferMatCap2ndTex = false;
            transferMatCap2ndBumpMask = false;

            shaderTransferSourceGameObject = null;
            shaderTransferTargetGameObjects?.Clear();
            shaderTransferSourceAvailableMaterials?.Clear();
            shaderTransferSelectedSourceMaterialIndex = -1;
            shaderTransferTargetAvailableMaterials?.Clear();
            shaderTransferTargetSelectedMaterials?.Clear();
            shaderTransferSourceMaterialToGameObjects?.Clear();
            shaderTransferTargetMaterialToGameObjects?.Clear();
            shaderTransferSourceMaterialFoldouts?.Clear();
            shaderTransferTargetMaterialFoldouts?.Clear();

            referenceGameObject = null;
            targetGameObject = null;
            referenceComponent = null;
            targetComponent = null;
            selectedReferenceMaterial = null;
            selectedTargetMaterial = null;

            enableMaterialTransfer = false;
            materialTransferDirection = 0;

            balanceGameObject = null;
            balanceRendererComponent = null;
            balanceSelectedMaterial = null;
            previousColor = Color.white;
            newColor = Color.white;
            balanceModeEnabled = true;
            transparentModeEnabled = false;
            balanceModeVersion = BalanceModeVersion.V1_Distance;

            texturePreviewScrollPosition = Vector2.zero;
            showTexturePreview = true;
            v1Weight = 1.0f;
            v1MinimumValue = 0.0f;
            v2Weight = 1.0f;
            v2Radius = 0.0f;
            v2MinimumValue = 0.0f;
            v2IncludeOutside = false;

            v3GradientColor = Color.white;
            v3GradientStart = 0f;
            v3GradientEnd = 100f;

            enableBrightness = false;
            brightness = 1.0f;
            enableContrast = false;
            contrast = 1.0f;
            enableGamma = false;
            gamma = 1.0f;
            enableExposure = false;
            exposure = 0.0f;
            enableTransparency = false;
            transparency = 0.0f;

            UpdateSmartColorMatchParameterCache();
            UpdateParameterCache();

            Repaint();
        }
        
        private Color ApplyColorAdjustments(Color color)
        {
            // Convert to HSV
            Color.RGBToHSV(color, out float h, out float s, out float v);
            
            // Apply hue shift
            h = (h + hueShift / 360f) % 1f;
            if (h < 0) h += 1f;
            
            // Apply saturation
            s = Mathf.Clamp01(s * saturationMultiplier);
            
            // Convert back to RGB
            Color adjustedColor = Color.HSVToRGB(h, s, v);
            
            // Apply brightness
            adjustedColor.r = Mathf.Clamp01(adjustedColor.r + brightnessOffset);
            adjustedColor.g = Mathf.Clamp01(adjustedColor.g + brightnessOffset);
            adjustedColor.b = Mathf.Clamp01(adjustedColor.b + brightnessOffset);
            
            // Apply contrast
            adjustedColor.r = Mathf.Clamp01((adjustedColor.r - 0.5f) * contrastMultiplier + 0.5f);
            adjustedColor.g = Mathf.Clamp01((adjustedColor.g - 0.5f) * contrastMultiplier + 0.5f);
            adjustedColor.b = Mathf.Clamp01((adjustedColor.b - 0.5f) * contrastMultiplier + 0.5f);
            
            adjustedColor.a = color.a; // Preserve alpha
            return adjustedColor;
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
            bool previewGenerated = false;
            
            try
            {
                // Create readable copies without modifying original textures
                var readableTarget = TextureProcessor.MakeReadableCopy(targetTexture);
                var readableReference = TextureProcessor.MakeReadableCopy(referenceTexture);

                // Validate textures and log format information
                Debug.Log($"Target texture: {targetTexture.name}, Format: {(readableTarget != null ? readableTarget.format.ToString() : "NULL")}, Size: {(readableTarget != null ? $"{readableTarget.width}x{readableTarget.height}" : "NULL")}");
                Debug.Log($"Reference texture: {referenceTexture.name}, Format: {(readableReference != null ? readableReference.format.ToString() : "NULL")}, Size: {(readableReference != null ? $"{readableReference.width}x{readableReference.height}" : "NULL")}");

                // Check if MakeReadableCopy failed
                if (readableTarget == null)
                {
                    throw new Exception($"Could not create readable copy of target texture '{targetTexture.name}'. The texture may be corrupted or in an unsupported format.");
                }
                if (readableReference == null)
                {
                    throw new Exception($"Could not create readable copy of reference texture '{referenceTexture.name}'. The texture may be corrupted or in an unsupported format.");
                }
                
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
                // Always update original texture to reflect current state
                UpdateOriginalTexture();
                
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
                    UnityEngine.Object.DestroyImmediate(previewTarget, true);
                if (previewReference != readableReference)
                    UnityEngine.Object.DestroyImmediate(previewReference, true);

                // Clean up readable copies
                if (readableTarget != null)
                    UnityEngine.Object.DestroyImmediate(readableTarget, true);
                if (readableReference != null)
                    UnityEngine.Object.DestroyImmediate(readableReference, true);

                processingProgress = 1f;
                showPreview = true;
                previewGenerated = previewTexture != null;
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

                if (previewGenerated)
                {
                    UpdateParameterCache();
                    directTabHasQueuedParameters = false;
                }
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
                // Create readable copies without modifying original textures
                var readableTarget = TextureProcessor.MakeReadableCopy(targetTexture);
                var readableReference = TextureProcessor.MakeReadableCopy(referenceTexture);
                
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
                        // Clear preview and original texture to force refresh with updated content
                        ClearPreview();
                        EditorUtility.DisplayDialog(LocalizationManager.Get("success_title"), LocalizationManager.Get("success_message"), LocalizationManager.Get("ok"));
                    }
                    else
                    {
                        EditorUtility.DisplayDialog(LocalizationManager.Get("error_title"), LocalizationManager.Get("error_save_message"), LocalizationManager.Get("ok"));
                    }
                    
                    UnityEngine.Object.DestroyImmediate(fullResolutionResult, true);
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
            
            // Create readable copy without modifying original texture
            var readableTexture = TextureProcessor.MakeReadableCopy(source);
            return readableTexture;
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
                // Determine source and target materials for transfer based on selected direction
                Material transferSourceMaterial = materialTransferDirection == 0 ? selectedReferenceMaterial : selectedTargetMaterial;
                Material transferTargetMaterial = materialTransferDirection == 0 ? selectedTargetMaterial : selectedReferenceMaterial;
                
                // Check if materials are liltoon (only needed if material transfer is enabled)
                if (enableMaterialTransfer && (!IsLiltoonMaterial(transferSourceMaterial) || !IsLiltoonMaterial(transferTargetMaterial)))
                {
                    string directionText = materialTransferDirection == 0 ? "参照用 → 変更対象" : "変更対象 → 参照用";
                    int dialogResult = EditorUtility.DisplayDialogComplex(
                        "警告", 
                        $"マテリアル設定転送が有効ですが、選択されたマテリアルがliltoonではありません。\n転送方向: {directionText}\n処理を続行しますか？",
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
                
                // Apply color adjustment process
                ApplyColorAdjustmentToMaterial(referenceTexture, targetTexture, selectedTargetMaterial, 
                    transferSourceMaterial, transferTargetMaterial);
                
                // The material transfer is now handled inside ApplyColorAdjustmentToMaterial
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
        
        private bool IsLiltoonMaterial(Material material)
        {
            return MaterialUnifyToolMethods.IsLiltoonMaterial(material);
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
        
        // Direct tab action buttons with high-precision support
        private void DrawDirectTabActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            float actionButtonHeight = EditorGUIUtility.singleLineHeight * 2f;
            
            bool canProcess = CanProcessExperimental();
            GUI.enabled = canProcess;
            if (GUILayout.Button(LocalizationManager.Get("generate_preview"), GUILayout.Height(actionButtonHeight)))
            {
                if (useHighPrecisionMode && GetExperimentalReferenceTexture() != null)
                {
                    GenerateHighPrecisionPreviewForDirectTab();
                }
                else
                {
                    GenerateExperimentalPreview();
                }
            }
            
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

        // Lazily create a 150% sized bold label for key headings.
        private GUIStyle GetLargeBoldLabelStyle()
        {
            if (largeBoldLabelStyle == null)
            {
                largeBoldLabelStyle = CreateScaledStyle(EditorStyles.boldLabel, 1.5f, FontStyle.Bold);
            }

            return largeBoldLabelStyle;
        }

        // Lazily create a 150% sized label used for prominent toggles.
        private GUIStyle GetLargeToggleLabelStyle()
        {
            if (largeToggleLabelStyle == null)
            {
                largeToggleLabelStyle = CreateScaledStyle(EditorStyles.label, 1.5f, null);
                largeToggleLabelStyle.alignment = TextAnchor.MiddleLeft;
            }

            return largeToggleLabelStyle;
        }

        private GUIStyle CreateScaledStyle(GUIStyle baseStyle, float scale, FontStyle? overrideFontStyle)
        {
            var style = new GUIStyle(baseStyle);

            int baseSize = baseStyle.fontSize;
            if (baseSize <= 0)
            {
                baseSize = EditorStyles.label.fontSize;
            }

            if (baseSize <= 0)
            {
                baseSize = 12;
            }

            style.fontSize = Mathf.RoundToInt(baseSize * scale);
            if (style.fontSize <= 0)
            {
                style.fontSize = Mathf.RoundToInt(12 * scale);
            }

            if (overrideFontStyle.HasValue)
            {
                style.fontStyle = overrideFontStyle.Value;
            }

            return style;
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
        
        private void ApplyColorAdjustmentToMaterial(Texture2D referenceTexture, Texture2D targetTexture, Material targetMaterial, 
            Material transferSourceMaterial = null, Material transferTargetMaterial = null)
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
                // Create readable copies without modifying original textures
                var readableTarget = TextureProcessor.MakeReadableCopy(targetTexture);
                var readableReference = TextureProcessor.MakeReadableCopy(referenceTexture);
                
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
                            
                            // Apply material transfer if enabled and both materials are liltoon
                            string successMessage = "テクスチャの調整が完了しました。";
                            
                            if (enableMaterialTransfer && transferSourceMaterial != null && transferTargetMaterial != null &&
                                IsLiltoonMaterial(transferSourceMaterial) && IsLiltoonMaterial(transferTargetMaterial))
                            {
                                try
                                {
                                    LiltoonPresetApplier.TransferDrawingEffects(transferSourceMaterial, transferTargetMaterial, 1.0f);
                                    string directionText = materialTransferDirection == 0 ? "参照用 → 変更対象" : "変更対象 → 参照用";
                                    successMessage = $"色調整とマテリアル設定転送が完了しました。\n転送方向: {directionText}";
                                }
                                catch (System.Exception e)
                                {
                                    Debug.LogError($"Material transfer failed: {e.Message}");
                                    successMessage = "テクスチャの調整は完了しましたが、マテリアル設定転送に失敗しました。";
                                }
                            }
                            
                            // Clear preview and original texture to force refresh with updated content
                            ClearPreview();
                            EditorUtility.DisplayDialog("成功", successMessage, "OK");
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
                    
                    UnityEngine.Object.DestroyImmediate(adjustedTexture, true);
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
        
        private bool HasSmartColorMatchParametersChanged()
        {
            if (lastTransformConfig == null) return true;
            
            return !lastFromColor.Equals(selectedFromColor) ||
                   !lastToColor.Equals(selectedToColor) ||
                   lastHasFromColor != hasSelectedFromColor ||
                   lastHasToColor != hasSelectedToColor ||
                   Math.Abs(lastTransformConfig.intensity - transformConfig.intensity) > 0.001f ||
                   Math.Abs(lastTransformConfig.brightness - transformConfig.brightness) > 0.001f ||
                   Math.Abs(lastTransformConfig.contrast - transformConfig.contrast) > 0.001f ||
                   Math.Abs(lastTransformConfig.gamma - transformConfig.gamma) > 0.001f ||
                   lastTransformConfig.balanceMode != transformConfig.balanceMode;
        }
        
        private void UpdateSmartColorMatchParameterCache()
        {
            lastTransformConfig = new ColorTransformConfig
            {
                intensity = transformConfig.intensity,
                brightness = transformConfig.brightness,
                contrast = transformConfig.contrast,
                gamma = transformConfig.gamma,
                transparency = transformConfig.transparency,
                balanceMode = transformConfig.balanceMode,
                selectionRadius = transformConfig.selectionRadius,
                minSimilarity = transformConfig.minSimilarity
            };
            
            lastFromColor = selectedFromColor;
            lastToColor = selectedToColor;
            lastHasFromColor = hasSelectedFromColor;
            lastHasToColor = hasSelectedToColor;
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
                 lastUseHighPrecisionModeForPreview != useHighPrecisionMode;
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
                bool highPrecision)
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
                       HighPrecision == other.HighPrecision;
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
                var readableTexture = TextureProcessor.MakeReadableCopy(singleTexture);

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
                var readableTexture = TextureProcessor.MakeReadableCopy(singleTexture);
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
                    
                    UnityEngine.Object.DestroyImmediate(result, true);
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
        
        // Smart Color Match UI methods
        
        private void DrawColorSelectionInterface()
        {
            EditorGUILayout.LabelField("🎨 スマートカラーマッチング", EditorStyles.boldLabel);
            
            // Color selection instructions
            EditorGUILayout.HelpBox("テクスチャをクリックして色を選択:\n• 変換元の色を選択 (FROM)\n• 変換先の色を選択 (TO)", MessageType.Info);
            
            // Display texture with click handling
            DrawInteractiveTexture();
            
            GUILayout.Space(10);
            
            // Color display and manual input
            DrawColorDisplayPanel();
        }
        
        private void DrawInteractiveTexture()
        {
            if (targetTexture == null) return;
            
            float maxSize = 300f;
            float aspectRatio = (float)targetTexture.width / targetTexture.height;
            float displayWidth = maxSize;
            float displayHeight = maxSize / aspectRatio;
            
            if (displayHeight > maxSize)
            {
                displayHeight = maxSize;
                displayWidth = maxSize * aspectRatio;
            }
            
            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            Rect textureRect = GUILayoutUtility.GetRect(displayWidth, displayHeight, GUILayout.Width(displayWidth), GUILayout.Height(displayHeight));
            
            // Draw texture
            EditorGUI.DrawTextureTransparent(textureRect, targetTexture);
            
            // Draw selection overlay if we have a selection mask
            if (selectionMask != null)
            {
                DrawSelectionOverlay(textureRect);
            }
            
            // Handle mouse interaction
            HandleTextureInteraction(textureRect);
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawColorDisplayPanel()
        {
            EditorGUILayout.BeginVertical("box");
            
            // FROM Color
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("FROM:", GUILayout.Width(50));
            
            if (hasSelectedFromColor)
            {
                GUI.color = selectedFromColor.ToColor();
                GUILayout.Box("", GUILayout.Width(30), GUILayout.Height(20));
                GUI.color = Color.white;
                
                EditorGUILayout.LabelField($"RGB({selectedFromColor.R}, {selectedFromColor.G}, {selectedFromColor.B})", EditorStyles.miniLabel);
                
                if (GUILayout.Button("❌", GUILayout.Width(20)))
                {
                    hasSelectedFromColor = false;
                    currentSelectionMode = SelectionMode.None;
                }
            }
            else
            {
                if (GUILayout.Button("Select FROM Color", GUILayout.Height(20)))
                {
                    currentSelectionMode = SelectionMode.FromColor;
                    EditorGUIUtility.SetWantsMouseJumping(1);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // TO Color  
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("TO:", GUILayout.Width(50));
            
            if (hasSelectedToColor)
            {
                GUI.color = selectedToColor.ToColor();
                GUILayout.Box("", GUILayout.Width(30), GUILayout.Height(20));
                GUI.color = Color.white;
                
                EditorGUILayout.LabelField($"RGB({selectedToColor.R}, {selectedToColor.G}, {selectedToColor.B})", EditorStyles.miniLabel);
                
                if (GUILayout.Button("❌", GUILayout.Width(20)))
                {
                    hasSelectedToColor = false;
                    currentSelectionMode = SelectionMode.None;
                }
            }
            else
            {
                if (GUILayout.Button("Select TO Color", GUILayout.Height(20)))
                {
                    currentSelectionMode = SelectionMode.ToColor;
                    EditorGUIUtility.SetWantsMouseJumping(1);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Manual color input
            GUILayout.Space(5);
            EditorGUILayout.LabelField("Manual Input:", EditorStyles.miniLabel);
            
            EditorGUILayout.BeginHorizontal();
            Color manualFromColor = hasSelectedFromColor ? selectedFromColor.ToColor() : Color.white;
            Color newFromColor = EditorGUILayout.ColorField("FROM", manualFromColor);
            if (newFromColor != manualFromColor)
            {
                selectedFromColor = new ColorPixel(newFromColor);
                hasSelectedFromColor = true;
                GeneratePreviewIfReady();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            Color manualToColor = hasSelectedToColor ? selectedToColor.ToColor() : Color.white;
            Color newToColor = EditorGUILayout.ColorField("TO", manualToColor);
            if (newToColor != manualToColor)
            {
                selectedToColor = new ColorPixel(newToColor);
                hasSelectedToColor = true;
                GeneratePreviewIfReady();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSimpleSettingsPanel()
        {
            EditorGUILayout.LabelField("⚙️ 設定", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical("box");
            
            // TexColAdjuster processing mode
            transformConfig.balanceMode = (ColorTransformConfig.BalanceMode)EditorGUILayout.EnumPopup("処理モード:", transformConfig.balanceMode);
            
            // Intensity
            transformConfig.intensity = EditorGUILayout.Slider("強度:", transformConfig.intensity, 0f, 1f);
            
            // Quick adjustments
            EditorGUILayout.BeginHorizontal();
            transformConfig.brightness = EditorGUILayout.Slider("明度:", transformConfig.brightness, 0.5f, 2f);
            if (GUILayout.Button("Reset", GUILayout.Width(50))) transformConfig.brightness = 1f;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            transformConfig.contrast = EditorGUILayout.Slider("コントラスト:", transformConfig.contrast, 0.5f, 2f);
            if (GUILayout.Button("Reset", GUILayout.Width(50))) transformConfig.contrast = 1f;
            EditorGUILayout.EndHorizontal();
            
            // Advanced settings (collapsible)
            GUILayout.Space(5);
            bool showAdvanced = EditorGUILayout.Foldout(EditorPrefs.GetBool("TexColAdjuster_ShowAdvanced", false), "高度な設定");
            EditorPrefs.SetBool("TexColAdjuster_ShowAdvanced", showAdvanced);
            
            if (showAdvanced)
            {
                EditorGUI.indentLevel++;
                transformConfig.selectionRadius = EditorGUILayout.Slider("選択範囲:", transformConfig.selectionRadius, 0.1f, 2f);
                transformConfig.minSimilarity = EditorGUILayout.Slider("最小類似度:", transformConfig.minSimilarity, 0f, 1f);
                transformConfig.gamma = EditorGUILayout.Slider("ガンマ:", transformConfig.gamma, 0.1f, 3f);
                transformConfig.transparency = EditorGUILayout.Slider("透明度:", transformConfig.transparency, 0f, 1f);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSmartColorMatchActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            
            // Area selection button
            GUI.enabled = targetTexture != null;
            if (GUILayout.Button("📍 領域選択", GUILayout.Height(25)))
            {
                currentSelectionMode = SelectionMode.AreaSelection;
                EditorGUIUtility.SetWantsMouseJumping(1);
            }
            
            // Clear selection
            GUI.enabled = selectionMask != null;
            if (GUILayout.Button("🧹 選択クリア", GUILayout.Height(25)))
            {
                selectionMask = null;
                GeneratePreviewIfReady();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            
            // Preview button
            GUI.enabled = CanProcessSmartColorMatch();
            if (GUILayout.Button("🔍 プレビュー生成", GUILayout.Height(30)))
            {
                GenerateColorChangerPreview();
            }
            
            // Apply button
            GUI.enabled = previewTexture != null;
            if (GUILayout.Button("✨ 変更を適用", GUILayout.Height(30)))
            {
                ApplyColorChangerAdjustment();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            // Real-time preview toggle
            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            realTimePreview = EditorGUILayout.Toggle("⚡ リアルタイムプレビュー", realTimePreview);
            if (realTimePreview && GUILayout.Button("?", GUILayout.Width(20)))
            {
                EditorUtility.DisplayDialog("リアルタイムプレビュー", "設定変更時に自動でプレビューを更新します。パフォーマンスが重い場合は無効にしてください。", "OK");
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawTexColAdjusterPreview()
        {
            EditorGUILayout.LabelField("🖼️ プレビュー", EditorStyles.boldLabel);
            
            if (previewTexture != null)
            {
                float maxSize = 400f;
                float aspectRatio = (float)previewTexture.width / previewTexture.height;
                float displayWidth = maxSize;
                float displayHeight = maxSize / aspectRatio;
                
                if (displayHeight > maxSize)
                {
                    displayHeight = maxSize;
                    displayWidth = maxSize * aspectRatio;
                }
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                
                // Before/After comparison
                EditorGUILayout.BeginVertical();
                
                if (originalTexture != null)
                {
                    EditorGUILayout.LabelField("変更前:", EditorStyles.centeredGreyMiniLabel);
                    GUILayout.Label(originalTexture, GUILayout.Width(displayWidth/2), GUILayout.Height(displayHeight/2));
                }
                
                EditorGUILayout.LabelField("変更後:", EditorStyles.centeredGreyMiniLabel);
                GUILayout.Label(previewTexture, GUILayout.Width(displayWidth/2), GUILayout.Height(displayHeight/2));
                
                EditorGUILayout.EndVertical();
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }
        
        // Helper methods for Smart Color Match interface
        
        private void HandleTextureInteraction(Rect textureRect)
        {
            Event currentEvent = Event.current;
            Vector2 mousePosition = currentEvent.mousePosition;
            
            if (textureRect.Contains(mousePosition))
            {
                // Calculate UV coordinates
                Vector2 localMouse = mousePosition - textureRect.position;
                Vector2 uv = new Vector2(localMouse.x / textureRect.width, 1f - (localMouse.y / textureRect.height));
                uv = Vector2.one - Vector2.Max(Vector2.zero, Vector2.one - uv);
                
                // Get pixel coordinates
                int pixelX = Mathf.FloorToInt(uv.x * targetTexture.width);
                int pixelY = Mathf.FloorToInt(uv.y * targetTexture.height);
                pixelX = Mathf.Clamp(pixelX, 0, targetTexture.width - 1);
                pixelY = Mathf.Clamp(pixelY, 0, targetTexture.height - 1);
                
                lastClickPosition = new Vector2Int(pixelX, pixelY);
                
                // Get hover color
                try
                {
                    var readableTexture = TextureProcessor.MakeReadableCopy(targetTexture);
                    if (readableTexture != null)
                    {
                        hoverColor = readableTexture.GetPixel(pixelX, pixelY);
                        
                        // Show enhanced color and mesh info tooltip
                        if (currentEvent.type == EventType.Repaint)
                        {
                            DrawEnhancedTooltip(mousePosition, hoverColor, pixelX, pixelY);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to get pixel color: {e.Message}");
                }
                
                // Handle clicks
                if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
                {
                    HandleColorSelection(pixelX, pixelY);
                    currentEvent.Use();
                    Repaint();
                }
                
                // Change cursor
                EditorGUIUtility.AddCursorRect(textureRect, MouseCursor.Text);
                
                if (currentEvent.type == EventType.MouseMove)
                {
                    Repaint();
                }
            }
        }

        private void DrawEnhancedTooltip(Vector2 mousePosition, Color hoverColor, int pixelX, int pixelY)
        {
            var tooltipContent = new List<string>();
            
            // Basic color information
            tooltipContent.Add($"RGB({(int)(hoverColor.r*255)}, {(int)(hoverColor.g*255)}, {(int)(hoverColor.b*255)})");
            tooltipContent.Add($"Alpha: {(int)(hoverColor.a*255)}");
            tooltipContent.Add($"Pixel: ({pixelX}, {pixelY})");
            
            // Add mesh information if available
            if (showMeshInfo && currentMeshInfo != null)
            {
                tooltipContent.Add(""); // Empty line
                tooltipContent.Add("Mesh Info:");
                tooltipContent.Add($"Triangles: {currentMeshInfo.triangleCount}");
                tooltipContent.Add($"Vertices: {currentMeshInfo.vertexCount}");
                if (!currentMeshInfo.usesTargetTexture)
                {
                    tooltipContent.Add("⚠️ Texture not used");
                }
            }
            
            // Calculate tooltip size
            var style = EditorStyles.helpBox;
            float maxWidth = 0;
            float totalHeight = 0;
            
            foreach (var line in tooltipContent)
            {
                if (string.IsNullOrEmpty(line))
                {
                    totalHeight += 5; // Empty line spacing
                    continue;
                }
                
                var size = style.CalcSize(new GUIContent(line));
                maxWidth = Mathf.Max(maxWidth, size.x);
                totalHeight += size.y;
            }
            
            maxWidth += 10; // Padding
            totalHeight += 10; // Padding
            
            // Position tooltip to avoid screen edges
            var tooltipRect = new Rect(mousePosition.x + 15, mousePosition.y - totalHeight - 10, maxWidth, totalHeight);
            
            if (tooltipRect.x + tooltipRect.width > Screen.width)
                tooltipRect.x = mousePosition.x - tooltipRect.width - 15;
            if (tooltipRect.y < 0)
                tooltipRect.y = mousePosition.y + 20;
            
            // Draw tooltip background
            GUI.Box(tooltipRect, "", EditorStyles.helpBox);
            
            // Draw tooltip content
            var contentRect = new Rect(tooltipRect.x + 5, tooltipRect.y + 5, tooltipRect.width - 10, tooltipRect.height - 10);
            var lineRect = new Rect(contentRect.x, contentRect.y, contentRect.width, EditorGUIUtility.singleLineHeight);
            
            foreach (var line in tooltipContent)
            {
                if (string.IsNullOrEmpty(line))
                {
                    lineRect.y += 5; // Empty line spacing
                    continue;
                }
                
                GUI.Label(lineRect, line, style);
                lineRect.y += EditorGUIUtility.singleLineHeight;
            }
        }

        private void UpdateCurrentMeshInfo()
        {
            if (referenceGameObject != null && referenceTexture != null)
            {
                int materialIndex = highPrecisionConfig != null ? highPrecisionConfig.materialIndex : 0;
                currentMeshInfo = UVMapGenerator.GetMeshInfo(referenceGameObject, referenceTexture, materialIndex);
                
                // Auto-update material index if auto-detection found a better match
                if (currentMeshInfo != null && highPrecisionConfig != null && 
                    currentMeshInfo.materialIndex != highPrecisionConfig.materialIndex)
                {
                    Debug.Log($"[High-precision] Auto-updating material index from {highPrecisionConfig.materialIndex} to {currentMeshInfo.materialIndex}");
                    highPrecisionConfig.materialIndex = currentMeshInfo.materialIndex;
                }
            }
            else
            {
                currentMeshInfo = null;
            }
        }
        
        private void HandleColorSelection(int pixelX, int pixelY)
        {
            Color selectedColor = hoverColor;
            ColorPixel colorPixel = new ColorPixel(selectedColor);
            
            switch (currentSelectionMode)
            {
                case SelectionMode.FromColor:
                    selectedFromColor = colorPixel;
                    hasSelectedFromColor = true;
                    currentSelectionMode = SelectionMode.None;
                    EditorGUIUtility.SetWantsMouseJumping(0);
                    GeneratePreviewIfReady();
                    break;
                    
                case SelectionMode.ToColor:
                    selectedToColor = colorPixel;
                    hasSelectedToColor = true;
                    currentSelectionMode = SelectionMode.None;
                    EditorGUIUtility.SetWantsMouseJumping(0);
                    GeneratePreviewIfReady();
                    break;
                    
                case SelectionMode.AreaSelection:
                    // Create flood-fill selection
                    selectionMask = DifferenceBasedProcessor.CreateFloodFillSelection(
                        targetTexture, pixelX, pixelY, transformConfig.selectionRadius * 0.1f);
                    currentSelectionMode = SelectionMode.None;
                    EditorGUIUtility.SetWantsMouseJumping(0);
                    GeneratePreviewIfReady();
                    break;
            }
        }
        
        private void DrawSelectionOverlay(Rect textureRect)
        {
            // This would draw the selection mask overlay
            // For now, we'll implement a simple approach
            if (selectionMask != null && targetTexture != null)
            {
                // Draw selection outline (simplified)
                GUI.color = new Color(0, 1, 1, 0.5f); // Cyan overlay
                GUI.Box(textureRect, "");
                GUI.color = Color.white;
            }
        }
        
        private bool CanProcessSmartColorMatch()
        {
            return targetTexture != null && hasSelectedFromColor && hasSelectedToColor;
        }
        
        // Legacy method stubs for compatibility
        private void CheckForExperimentalAutoPreview()
        {
            if (GetExperimentalReferenceTexture() != null && GetExperimentalTargetTexture() != null && previewTexture == null)
            {
                // Auto-generate preview when both main color textures are available
                EditorApplication.delayCall += () => {
                    if (CanProcessExperimental())
                    {
                        if (useHighPrecisionMode && GetExperimentalReferenceTexture() != null)
                        {
                            GenerateHighPrecisionPreviewForDirectTab();
                        }
                        else
                        {
                            GenerateExperimentalPreview();
                        }
                    }
                };
            }
        }
        
        private void GenerateColorChangerPreview()
        {
            GenerateSmartColorMatchPreview();
        }
        
        private void ApplyColorChangerAdjustment()
        {
            ApplySmartColorMatchAdjustment();
        }
        
        private bool CanProcessColorChanger()
        {
            return CanProcessSmartColorMatch();
        }
        
        // High-precision processing methods for Direct tab
        private void GenerateHighPrecisionPreviewForDirectTab()
        {
            var referenceTexture = GetExperimentalReferenceTexture();
            var targetTexture = GetExperimentalTargetTexture();
            
            if (referenceTexture == null || targetTexture == null)
            {
                EditorUtility.DisplayDialog("エラー", "参照テクスチャまたはターゲットテクスチャが見つかりません。", "OK");
                return;
            }
            
            if (!HighPrecisionProcessor.ValidateHighPrecisionConfig(highPrecisionConfig, referenceTexture))
            {
                EditorUtility.DisplayDialog("エラー", "高精度モードの設定が不完全です。", "OK");
                return;
            }
            
            isProcessing = true;
            processingProgress = 0f;
            bool previewGenerated = false;
            
            try
            {
                // Always update original texture to reflect current state
                UpdateOriginalTexture();
                
                processingProgress = 0.3f;
                Repaint();
                
                // Generate high precision reference preview (masked texture for eyedropper)
                highPrecisionPreviewTexture = HighPrecisionProcessor.CreateHighPrecisionPreview(
                    referenceTexture, highPrecisionConfig, true);
                
                processingProgress = 0.6f;
                Repaint();
                
                // Use high-precision mode for direct tab
                previewTexture = HighPrecisionProcessor.ProcessWithHighPrecision(
                    targetTexture, referenceTexture, highPrecisionConfig, 
                    adjustmentIntensity, preserveLuminance, adjustmentMode);
                
                processingProgress = 1f;
                showPreview = true;
                previewGenerated = previewTexture != null;
                
                if (previewTexture == null)
                {
                    EditorUtility.DisplayDialog("エラー", "高精度プレビューの生成に失敗しました。テクスチャまたはマテリアル設定を確認してください。", "OK");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"High-precision preview generation failed for direct tab: {e.Message}");
                EditorUtility.DisplayDialog("エラー", $"高精度プレビューエラー: {e.Message}", "OK");
            }
            finally
            {
                isProcessing = false;
                Repaint();

                if (previewGenerated)
                {
                    UpdateParameterCache();
                    directTabHasQueuedParameters = false;
                }
            }
        }
        
        private void ApplyHighPrecisionAdjustmentForDirectTab()
        {
            if (previewTexture == null)
            {
                EditorUtility.DisplayDialog("エラー", "適用するプレビューがありません。", "OK");
                return;
            }
            
            var targetTexture = GetExperimentalTargetTexture();
            var referenceTexture = GetExperimentalReferenceTexture();
            
            if (targetTexture == null || referenceTexture == null)
            {
                EditorUtility.DisplayDialog("エラー", "テクスチャが見つかりません。", "OK");
                return;
            }
            
            // Check for material transfer requirements if enabled
            if (enableMaterialTransfer && (!IsLiltoonMaterial(selectedReferenceMaterial) || !IsLiltoonMaterial(selectedTargetMaterial)))
            {
                string directionText = materialTransferDirection == 0 ? "参照用 → 変更対象" : "変更対象 → 参照用";
                int materialDialogResult = EditorUtility.DisplayDialogComplex(
                    "警告", 
                    $"マテリアル設定転送が有効ですが、選択されたマテリアルがliltoonではありません。\n転送方向: {directionText}\n処理を続行しますか？",
                    "続行", "キャンセル", ""
                );
                if (materialDialogResult != 0) return; // Cancel or closed with X
            }
            
            string originalPath = AssetDatabase.GetAssetPath(targetTexture);
            var saveOptions = SaveOptionsExtensions.GetDefaultVRChatSettings();
            
            int dialogResult = EditorUtility.DisplayDialogComplex(
                "高精度色変換を適用 (直接指定)",
                "高精度モードでの変更を保存しますか？",
                "新しいファイルとして保存",
                "元ファイルを上書き",
                "キャンセル"
            );
            
            if (dialogResult == 2) return;
            
            saveOptions.overwriteOriginal = (dialogResult == 1);
            
            try
            {
                var fullResResult = HighPrecisionProcessor.ProcessWithHighPrecision(
                    targetTexture, referenceTexture, highPrecisionConfig,
                    adjustmentIntensity, preserveLuminance, adjustmentMode);
                
                if (fullResResult != null)
                {
                    if (TextureExporter.SaveTexture(fullResResult, originalPath, saveOptions))
                    {
                        AssetDatabase.Refresh();
                        
                        // Get the saved texture path and load it as an asset
                        string savedPath = GetSavePathForTexture(originalPath, saveOptions);
                        Texture2D savedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(savedPath);
                        
                        if (savedTexture != null && selectedTargetMaterial != null)
                        {
                            // Only apply the saved texture to the material if it's a different texture
                            // (i.e., saved as new file, not overwritten)
                            if (!saveOptions.overwriteOriginal)
                            {
                                // Apply the saved texture to the material
                                selectedTargetMaterial.SetTexture("_MainTex", savedTexture);
                                
                                // Mark material as dirty to ensure it gets saved
                                EditorUtility.SetDirty(selectedTargetMaterial);
                                AssetDatabase.SaveAssets();
                            }
                            
                            // Apply material transfer if enabled and both materials are liltoon
                            if (enableMaterialTransfer && selectedReferenceMaterial != null && selectedTargetMaterial != null &&
                                IsLiltoonMaterial(selectedReferenceMaterial) && IsLiltoonMaterial(selectedTargetMaterial))
                            {
                                // Determine source and target materials for transfer based on selected direction
                                Material transferSourceMaterial = materialTransferDirection == 0 ? selectedReferenceMaterial : selectedTargetMaterial;
                                Material transferTargetMaterial = materialTransferDirection == 0 ? selectedTargetMaterial : selectedReferenceMaterial;
                                
                                string directionText = materialTransferDirection == 0 ? "参照用 → 変更対象" : "変更対象 → 参照用";
                                LiltoonPresetApplier.TransferDrawingEffects(transferSourceMaterial, transferTargetMaterial, 1.0f);
                                // Clear preview and original texture to force refresh with updated content
                                ClearPreview();
                                EditorUtility.DisplayDialog("成功", $"高精度色変換とマテリアル設定転送が完了しました。\n転送方向: {directionText}", "OK");
                            }
                            else
                            {
                                // Clear preview and original texture to force refresh with updated content
                                ClearPreview();
                                EditorUtility.DisplayDialog("成功", "高精度色変換が正常に適用されました！", "OK");
                            }
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("成功", "高精度色変換が正常に適用されました！", "OK");
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("エラー", "テクスチャの保存に失敗しました。", "OK");
                    }
                    
                    UnityEngine.Object.DestroyImmediate(fullResResult, true);
                }
                else
                {
                    EditorUtility.DisplayDialog("エラー", "高精度色変換処理に失敗しました。", "OK");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"High-precision adjustment application failed for direct tab: {e.Message}");
                EditorUtility.DisplayDialog("エラー", $"高精度色変換エラー: {e.Message}", "OK");
            }
        }
        
        private void DrawColorChangerActionButtons()
        {
            DrawSmartColorMatchActionButtons();
        }
        
        private void GenerateSmartColorMatchPreview()
        {
            if (!CanProcessColorChanger()) return;
            
            isProcessing = true;
            processingProgress = 0f;
            
            try
            {
                // Always update original texture to reflect current state
                UpdateOriginalTexture();
                
                processingProgress = 0.3f;
                Repaint();
                
                // Use TexColAdjuster's intelligent difference-based processor
                previewTexture = PerformanceOptimizedProcessor.ProcessIncremental(
                    targetTexture, selectedFromColor, selectedToColor, transformConfig, selectionMask);
                
                processingProgress = 1f;
                showPreview = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error generating Smart Color Match preview: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Preview generation failed: {e.Message}", "OK");
            }
            finally
            {
                isProcessing = false;
                Repaint();
            }
        }
        
        private void GeneratePreviewIfReady()
        {
            if (realTimePreview && CanProcessSmartColorMatch())
            {
                EditorApplication.delayCall += () => {
                    if (CanProcessSmartColorMatch())
                    {
                        GenerateSmartColorMatchPreview();
                    }
                };
            }
        }
        
        private void ApplySmartColorMatchAdjustment()
        {
            if (previewTexture == null)
            {
                EditorUtility.DisplayDialog("No Preview", "Please generate a preview first.", "OK");
                return;
            }
            
            string originalPath = AssetDatabase.GetAssetPath(targetTexture);
            var saveOptions = SaveOptionsExtensions.GetDefaultVRChatSettings();
            
            int dialogResult = EditorUtility.DisplayDialogComplex(
                "Apply Color Changes",
                "How would you like to save the changes?",
                "Save as New File",
                "Overwrite Original",
                "Cancel"
            );
            
            if (dialogResult == 2) return; // Cancel
            
            saveOptions.overwriteOriginal = (dialogResult == 1);
            
            try
            {
                // Process at full resolution
                var fullResResult = DifferenceBasedProcessor.ProcessTexture(
                    targetTexture, selectedFromColor, selectedToColor, transformConfig, selectionMask);
                
                if (fullResResult != null)
                {
                    if (TextureExporter.SaveTexture(fullResResult, originalPath, saveOptions))
                    {
                        EditorUtility.DisplayDialog("Success", "Color changes applied successfully!", "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Error", "Failed to save texture.", "OK");
                    }
                    
                    UnityEngine.Object.DestroyImmediate(fullResResult, true);
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Failed to process texture.", "OK");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error applying Smart Color Match adjustment: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Processing failed: {e.Message}", "OK");
            }
        }
        
        private void ResetColorSelection()
        {
            hasSelectedFromColor = false;
            hasSelectedToColor = false;
            currentSelectionMode = SelectionMode.None;
            selectionMask = null;
            ClearPreview();
        }
        
        private void OnDestroy()
        {
            ClearPreview();
        }
        
        // Color Adjust (ColorChanger Style) helper methods
        private void DrawMaterialSelectionForColorAdjust()
        {
            EditorGUILayout.LabelField("📁 Material Selection", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("GameObject:", GUILayout.Width(100));
            var newGameObject = EditorGUILayout.ObjectField(
                balanceGameObject, 
                typeof(GameObject), 
                true
            ) as GameObject;
            
            if (newGameObject != balanceGameObject)
            {
                balanceGameObject = newGameObject;
                balanceRendererComponent = GetRendererComponent(balanceGameObject);
                balanceSelectedMaterial = null;
                ClearPreview();
            }
            EditorGUILayout.EndHorizontal();
            
            if (balanceGameObject != null && balanceRendererComponent != null)
            {
                EditorGUILayout.LabelField("Material:", EditorStyles.boldLabel);
                DrawMaterialSelectionForBalance();
            }
        }
        
        private void DrawMaterialSelectionForBalance()
        {
            var materials = ExtractMaterials(balanceRendererComponent);
            if (materials == null || materials.Length == 0)
            {
                EditorGUILayout.HelpBox("No materials found on the renderer component.", MessageType.Warning);
                return;
            }
            
            if (materials.Length == 1)
            {
                var material = materials[0];
                if (balanceSelectedMaterial != material)
                {
                    balanceSelectedMaterial = material;
                    ClearPreview();
                }
                EditorGUILayout.LabelField($"Material: {material.name}", EditorStyles.miniLabel);
            }
            else
            {
                // Multiple materials - show dropdown
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Select Material:", GUILayout.Width(100));
                
                string[] materialNames = System.Array.ConvertAll(materials, mat => mat != null ? mat.name : "null");
                int currentIndex = System.Array.IndexOf(materials, balanceSelectedMaterial);
                if (currentIndex == -1) currentIndex = 0;
                
                int newIndex = EditorGUILayout.Popup(currentIndex, materialNames);
                if (newIndex != currentIndex && newIndex >= 0 && newIndex < materials.Length)
                {
                    balanceSelectedMaterial = materials[newIndex];
                    ClearPreview();
                }
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void DrawColorSelectionSection()
        {
            EditorGUILayout.LabelField("🎨 Color Selection (ColorChanger Style)", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            // Previous Color (Source)
            EditorGUILayout.BeginVertical(GUILayout.Width(150));
            EditorGUILayout.LabelField("Previous Color:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("(Click texture to pick)", EditorStyles.miniLabel);
            
            Rect previousColorRect = GUILayoutUtility.GetRect(120, 40);
            EditorGUI.DrawRect(previousColorRect, previousColor);
            
            EditorGUILayout.LabelField($"RGB({previousColor.r:F2}, {previousColor.g:F2}, {previousColor.b:F2})", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(20);
            
            // New Color (Target)
            EditorGUILayout.BeginVertical(GUILayout.Width(150));
            EditorGUILayout.LabelField("New Color:", EditorStyles.boldLabel);
            newColor = EditorGUILayout.ColorField("", newColor, GUILayout.Height(40));
            EditorGUILayout.LabelField($"RGB({newColor.r:F2}, {newColor.g:F2}, {newColor.b:F2})", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            // Mode toggles
            EditorGUILayout.BeginHorizontal();
            balanceModeEnabled = EditorGUILayout.Toggle("Balance Mode", balanceModeEnabled);
            transparentModeEnabled = EditorGUILayout.Toggle("Transparent Mode", transparentModeEnabled);
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawTexturePreviewForColorPicking(Texture2D texture)
        {
            if (texture == null) return;
            
            EditorGUILayout.LabelField("🖼️ Texture Preview - Click to Pick Previous Color", EditorStyles.boldLabel);
            
            float maxSize = 400f;
            float aspectRatio = (float)texture.width / texture.height;
            float displayWidth = maxSize;
            float displayHeight = maxSize / aspectRatio;
            
            if (displayHeight > maxSize)
            {
                displayHeight = maxSize;
                displayWidth = maxSize * aspectRatio;
            }
            
            // Scrollable texture preview
            texturePreviewScrollPosition = EditorGUILayout.BeginScrollView(
                texturePreviewScrollPosition, 
                GUILayout.Height(Mathf.Min(displayHeight + 20, 300f)),
                GUILayout.Width(displayWidth + 20)
            );
            
            Rect textureRect = GUILayoutUtility.GetRect(displayWidth, displayHeight, 
                GUILayout.Width(displayWidth), GUILayout.Height(displayHeight));
            
            EditorGUI.DrawTextureTransparent(textureRect, texture);
            HandleTextureClick(textureRect, texture);
            
            EditorGUILayout.EndScrollView();
        }
        
        private void HandleTextureClick(Rect textureRect, Texture2D texture)
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
                int pixelX = Mathf.FloorToInt(uv.x * texture.width);
                int pixelY = Mathf.FloorToInt(uv.y * texture.height);
                
                // Clamp pixel coordinates
                pixelX = Mathf.Clamp(pixelX, 0, texture.width - 1);
                pixelY = Mathf.Clamp(pixelY, 0, texture.height - 1);
                
                // Change cursor to eyedropper
                EditorGUIUtility.AddCursorRect(textureRect, MouseCursor.Text);
                
                // Handle click to pick color
                if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
                {
                    try
                    {
                        var readableTexture = TextureProcessor.MakeReadableCopy(texture);
                        if (readableTexture != null)
                        {
                            previousColor = readableTexture.GetPixel(pixelX, pixelY);
                            currentEvent.Use();
                            Repaint();
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Failed to pick color from texture: {e.Message}");
                    }
                }
            }
        }
        
        private void DrawSimplifiedBalanceSettings()
        {
            if (!balanceModeEnabled)
            {
                EditorGUILayout.HelpBox("Balance mode is disabled. Colors will be replaced directly.", MessageType.Info);
                return;
            }
            
            EditorGUILayout.LabelField("⚖️ Balance Settings", EditorStyles.boldLabel);
            
            // Balance mode version selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Algorithm:", GUILayout.Width(80));
            balanceModeVersion = (BalanceModeVersion)EditorGUILayout.EnumPopup(balanceModeVersion);
            EditorGUILayout.EndHorizontal();
            
            // Show current algorithm description
            switch (balanceModeVersion)
            {
                case BalanceModeVersion.V1_Distance:
                    EditorGUILayout.HelpBox("V1: RGB distance-based balance calculation", MessageType.None);
                    break;
                case BalanceModeVersion.V2_Radius:
                    EditorGUILayout.HelpBox("V2: Radius-based color selection", MessageType.None);
                    break;
                case BalanceModeVersion.V3_Gradient:
                    EditorGUILayout.HelpBox("V3: Grayscale gradient transformation", MessageType.None);
                    break;
            }
        }
        
        private void DrawBalanceModeSettings()
        {
            EditorGUILayout.LabelField("⚖️ Balance Mode Settings", EditorStyles.boldLabel);
            
            // Enable balance mode toggle
            useBalanceMode = EditorGUILayout.Toggle("Enable Balance Mode", useBalanceMode);
            
            if (!useBalanceMode)
            {
                EditorGUILayout.HelpBox("Balance mode disabled. Will use standard color processing.", MessageType.Info);
                return;
            }
            
            // Balance mode version selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Balance Version:", GUILayout.Width(120));
            balanceModeVersion = (BalanceModeVersion)EditorGUILayout.EnumPopup(balanceModeVersion);
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            // Mode-specific settings
            switch (balanceModeVersion)
            {
                case BalanceModeVersion.V1_Distance:
                    DrawV1Settings();
                    break;
                case BalanceModeVersion.V2_Radius:
                    DrawV2Settings();
                    break;
                case BalanceModeVersion.V3_Gradient:
                    DrawV3Settings();
                    break;
            }
        }
        
        private void DrawV1Settings()
        {
            EditorGUILayout.LabelField("V1 - Distance-based balance", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Uses RGB intersection distance for color balance calculation.", MessageType.Info);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Weight:", GUILayout.Width(80));
            v1Weight = EditorGUILayout.Slider(v1Weight, 0f, 2f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Min Value:", GUILayout.Width(80));
            v1MinimumValue = EditorGUILayout.Slider(v1MinimumValue, 0f, 1f);
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawV2Settings()
        {
            EditorGUILayout.LabelField("V2 - Radius-based balance", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Uses radius-based color selection with distance calculation.", MessageType.Info);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Weight:", GUILayout.Width(80));
            v2Weight = EditorGUILayout.Slider(v2Weight, 0f, 2f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Radius:", GUILayout.Width(80));
            v2Radius = EditorGUILayout.Slider(v2Radius, 0f, 255f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Min Value:", GUILayout.Width(80));
            v2MinimumValue = EditorGUILayout.Slider(v2MinimumValue, 0f, 1f);
            EditorGUILayout.EndHorizontal();
            
            v2IncludeOutside = EditorGUILayout.Toggle("Include Outside", v2IncludeOutside);
        }
        
        private void DrawV3Settings()
        {
            EditorGUILayout.LabelField("V3 - Gradient-based balance", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Uses grayscale calculation with gradient color transformation.", MessageType.Info);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Gradient Color:", GUILayout.Width(100));
            v3GradientColor = EditorGUILayout.ColorField(v3GradientColor);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Start:", GUILayout.Width(80));
            v3GradientStart = EditorGUILayout.Slider(v3GradientStart, 0f, 255f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("End:", GUILayout.Width(80));
            v3GradientEnd = EditorGUILayout.Slider(v3GradientEnd, 0f, 255f);
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawAdvancedColorSettings()
        {
            EditorGUILayout.LabelField("🎨 Advanced Color Configuration", EditorStyles.boldLabel);
            
            // Brightness
            EditorGUILayout.BeginHorizontal();
            enableBrightness = EditorGUILayout.Toggle(enableBrightness, GUILayout.Width(20));
            EditorGUILayout.LabelField("Brightness:", GUILayout.Width(80));
            GUI.enabled = enableBrightness;
            brightness = EditorGUILayout.Slider(brightness, 0f, 3f);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            // Contrast
            EditorGUILayout.BeginHorizontal();
            enableContrast = EditorGUILayout.Toggle(enableContrast, GUILayout.Width(20));
            EditorGUILayout.LabelField("Contrast:", GUILayout.Width(80));
            GUI.enabled = enableContrast;
            contrast = EditorGUILayout.Slider(contrast, 0f, 3f);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            // Gamma
            EditorGUILayout.BeginHorizontal();
            enableGamma = EditorGUILayout.Toggle(enableGamma, GUILayout.Width(20));
            EditorGUILayout.LabelField("Gamma:", GUILayout.Width(80));
            GUI.enabled = enableGamma;
            gamma = EditorGUILayout.Slider(gamma, 0.1f, 3f);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            // Exposure
            EditorGUILayout.BeginHorizontal();
            enableExposure = EditorGUILayout.Toggle(enableExposure, GUILayout.Width(20));
            EditorGUILayout.LabelField("Exposure:", GUILayout.Width(80));
            GUI.enabled = enableExposure;
            exposure = EditorGUILayout.Slider(exposure, -2f, 2f);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            // Transparency
            EditorGUILayout.BeginHorizontal();
            enableTransparency = EditorGUILayout.Toggle(enableTransparency, GUILayout.Width(20));
            EditorGUILayout.LabelField("Transparency:", GUILayout.Width(80));
            GUI.enabled = enableTransparency;
            transparency = EditorGUILayout.Slider(transparency, 0f, 1f);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawBalanceActions()
        {
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = CanProcessBalance();
            if (GUILayout.Button("Generate Preview"))
            {
                GenerateBalancePreview();
            }
            
            if (GUILayout.Button("Apply Balance"))
            {
                ApplyBalance();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
        }
        
        private bool CanProcessBalance()
        {
            return balanceSelectedMaterial != null && 
                   GetMainTexture(balanceSelectedMaterial) != null;
        }
        
        private void GenerateBalancePreview()
        {
            if (!CanProcessBalance()) return;
            
            var materialTexture = GetMainTexture(balanceSelectedMaterial);
            if (materialTexture == null) return;
            
            isProcessing = true;
            processingProgress = 0f;
            
            try
            {
                // Update original texture
                UpdateOriginalTexture();
                
                processingProgress = 0.3f;
                Repaint();
                
                // Apply balance processing
                previewTexture = ProcessBalanceAdjustment(materialTexture);
                
                processingProgress = 1f;
                showPreview = true;
                
                if (previewTexture == null)
                {
                    EditorUtility.DisplayDialog("Error", "Balance preview generation failed.", "OK");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Balance preview generation failed: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Preview generation error: {e.Message}", "OK");
            }
            finally
            {
                isProcessing = false;
                Repaint();
            }
        }
        
        private void ApplyBalance()
        {
            if (!CanProcessBalance()) return;
            
            var materialTexture = GetMainTexture(balanceSelectedMaterial);
            if (materialTexture == null) return;
            
            string originalPath = AssetDatabase.GetAssetPath(materialTexture);
            var saveOptions = SaveOptionsExtensions.GetDefaultVRChatSettings();
            
            int dialogResult = EditorUtility.DisplayDialogComplex(
                "Apply Balance Adjustment", 
                "Save the balance-adjusted texture?", 
                "Save as New", 
                "Overwrite Original",
                "Cancel"
            );
            
            if (dialogResult == 2) return; // Cancel
            
            saveOptions.overwriteOriginal = (dialogResult == 1);
            
            isProcessing = true;
            processingProgress = 0f;
            
            try
            {
                processingProgress = 0.3f;
                Repaint();
                
                var resultTexture = ProcessBalanceAdjustment(materialTexture);
                
                processingProgress = 0.8f;
                Repaint();
                
                if (resultTexture != null)
                {
                    if (TextureExporter.SaveTexture(resultTexture, originalPath, saveOptions))
                    {
                        AssetDatabase.Refresh();
                        
                        // Update material texture if saved as new file
                        if (!saveOptions.overwriteOriginal)
                        {
                            string savedPath = GetSavePathForTexture(originalPath, saveOptions);
                            Texture2D savedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(savedPath);
                            
                            if (savedTexture != null)
                            {
                                balanceSelectedMaterial.SetTexture("_MainTex", savedTexture);
                                EditorUtility.SetDirty(balanceSelectedMaterial);
                                AssetDatabase.SaveAssets();
                            }
                        }
                        
                        ClearPreview();
                        EditorUtility.DisplayDialog("Success", "Balance adjustment applied successfully!", "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Error", "Failed to save texture.", "OK");
                    }
                    
                    UnityEngine.Object.DestroyImmediate(resultTexture, true);
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Balance processing failed.", "OK");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Balance application failed: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Balance error: {e.Message}", "OK");
            }
            finally
            {
                isProcessing = false;
                Repaint();
            }
        }
        
        // Core balance processing based on Color-Changer's balance algorithms
        private Texture2D ProcessBalanceAdjustment(Texture2D sourceTexture)
        {
            if (sourceTexture == null) return null;

            var readableTexture = TextureProcessor.MakeReadableCopy(sourceTexture);
            if (readableTexture == null) return null;
            
            Color[] sourcePixels = TextureUtils.GetPixelsSafe(readableTexture);
            if (sourcePixels == null) return null;
            
            Color[] resultPixels = new Color[sourcePixels.Length];
            
            for (int i = 0; i < sourcePixels.Length; i++)
            {
                Color originalPixel = sourcePixels[i];
                Color resultPixel = originalPixel;
                
                // Skip transparent pixels
                if (originalPixel.a < ColorAdjuster.ALPHA_THRESHOLD)
                {
                    resultPixels[i] = originalPixel;
                    continue;
                }
                
                // Apply balance adjustment if enabled (ColorChanger style)
                if (balanceModeEnabled)
                {
                    resultPixel = ApplyColorChangerBalanceAdjustment(originalPixel, previousColor, newColor);
                }
                else
                {
                    // Direct color replacement when balance mode is disabled
                    if (IsColorSimilar(originalPixel, previousColor, 0.1f))
                    {
                        resultPixel = newColor;
                        resultPixel.a = originalPixel.a; // Preserve alpha
                    }
                }
                
                // Apply advanced color adjustments
                resultPixel = ApplyAdvancedColorAdjustment(resultPixel);
                
                resultPixels[i] = resultPixel;
            }
            
            var resultTexture = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
            if (TextureUtils.SetPixelsSafe(resultTexture, resultPixels))
            {
                return resultTexture;
            }
            
            UnityEngine.Object.DestroyImmediate(resultTexture, true);
            return null;
        }
        
        // ColorChanger-style balance adjustment
        private Color ApplyColorChangerBalanceAdjustment(Color originalColor, Color sourceColor, Color targetColor)
        {
            switch (balanceModeVersion)
            {
                case BalanceModeVersion.V1_Distance:
                    return ApplyV1ColorChangerBalance(originalColor, sourceColor, targetColor);
                case BalanceModeVersion.V2_Radius:
                    return ApplyV2ColorChangerBalance(originalColor, sourceColor, targetColor);
                case BalanceModeVersion.V3_Gradient:
                    return ApplyV3ColorChangerBalance(originalColor, sourceColor, targetColor);
                default:
                    return originalColor;
            }
        }
        
        private Color ApplyV1ColorChangerBalance(Color originalColor, Color sourceColor, Color targetColor)
        {
            // V1: Calculate color difference and apply based on RGB intersection distance
            Vector3 colorDifference = new Vector3(
                targetColor.r - sourceColor.r,
                targetColor.g - sourceColor.g,
                targetColor.b - sourceColor.b
            );
            
            // Calculate distance from source color
            float distance = Vector3.Distance(
                new Vector3(originalColor.r, originalColor.g, originalColor.b),
                new Vector3(sourceColor.r, sourceColor.g, sourceColor.b)
            );
            
            // Apply weight based on distance (closer colors get more adjustment)
            float adjustmentFactor = Mathf.Lerp(1f, v1MinimumValue, distance) * v1Weight;
            
            Vector3 adjustedColor = new Vector3(originalColor.r, originalColor.g, originalColor.b) + 
                                   colorDifference * adjustmentFactor;
            
            return new Color(
                Mathf.Clamp01(adjustedColor.x),
                Mathf.Clamp01(adjustedColor.y),
                Mathf.Clamp01(adjustedColor.z),
                originalColor.a
            );
        }
        
        private Color ApplyV2ColorChangerBalance(Color originalColor, Color sourceColor, Color targetColor)
        {
            // V2: Radius-based color selection with ColorChanger logic
            Vector3 originalVec = new Vector3(originalColor.r * 255f, originalColor.g * 255f, originalColor.b * 255f);
            Vector3 sourceVec = new Vector3(sourceColor.r * 255f, sourceColor.g * 255f, sourceColor.b * 255f);
            
            float distance = Vector3.Distance(originalVec, sourceVec);
            
            if (distance <= v2Radius || v2IncludeOutside)
            {
                Vector3 colorDifference = new Vector3(
                    targetColor.r - sourceColor.r,
                    targetColor.g - sourceColor.g,
                    targetColor.b - sourceColor.b
                );
                
                float adjustmentFactor = distance <= v2Radius ? 
                    Mathf.Lerp(1f, v2MinimumValue, distance / v2Radius) * v2Weight :
                    v2MinimumValue * v2Weight;
                
                Vector3 adjustedColor = new Vector3(originalColor.r, originalColor.g, originalColor.b) + 
                                       colorDifference * adjustmentFactor;
                
                return new Color(
                    Mathf.Clamp01(adjustedColor.x),
                    Mathf.Clamp01(adjustedColor.y),
                    Mathf.Clamp01(adjustedColor.z),
                    originalColor.a
                );
            }
            
            return originalColor;
        }
        
        private Color ApplyV3ColorChangerBalance(Color originalColor, Color sourceColor, Color targetColor)
        {
            // V3: Grayscale-based gradient transformation
            float grayscale = 0.299f * originalColor.r + 0.587f * originalColor.g + 0.114f * originalColor.b;
            float grayscaleValue = grayscale * 255f;
            
            if (grayscaleValue >= v3GradientStart && grayscaleValue <= v3GradientEnd)
            {
                Vector3 colorDifference = new Vector3(
                    targetColor.r - sourceColor.r,
                    targetColor.g - sourceColor.g,
                    targetColor.b - sourceColor.b
                );
                
                // Use gradient position as adjustment factor
                float t = (grayscaleValue - v3GradientStart) / (v3GradientEnd - v3GradientStart);
                
                Vector3 adjustedColor = new Vector3(originalColor.r, originalColor.g, originalColor.b) + 
                                       colorDifference * t;
                
                return new Color(
                    Mathf.Clamp01(adjustedColor.x),
                    Mathf.Clamp01(adjustedColor.y),
                    Mathf.Clamp01(adjustedColor.z),
                    originalColor.a
                );
            }
            
            return originalColor;
        }
        
        private bool IsColorSimilar(Color a, Color b, float threshold)
        {
            float distance = Vector3.Distance(
                new Vector3(a.r, a.g, a.b),
                new Vector3(b.r, b.g, b.b)
            );
            return distance <= threshold;
        }
        
        // Balance color adjustment based on Color-Changer algorithms
        private Color ApplyBalanceColorAdjustment(Color originalColor)
        {
            switch (balanceModeVersion)
            {
                case BalanceModeVersion.V1_Distance:
                    return ApplyV1BalanceAdjustment(originalColor);
                case BalanceModeVersion.V2_Radius:
                    return ApplyV2BalanceAdjustment(originalColor);
                case BalanceModeVersion.V3_Gradient:
                    return ApplyV3BalanceAdjustment(originalColor);
                default:
                    return originalColor;
            }
        }
        
        private Color ApplyV1BalanceAdjustment(Color color)
        {
            // V1: RGB intersection distance calculation
            float r = color.r * 255f;
            float g = color.g * 255f;
            float b = color.b * 255f;
            
            // Calculate distance-based adjustment factor
            float distance = Mathf.Sqrt((r * r + g * g + b * b) / 3f);
            float adjustmentFactor = Mathf.Lerp(v1MinimumValue, 1f, distance / 255f) * v1Weight;
            
            Color adjustedColor = new Color(
                Mathf.Clamp01(color.r * adjustmentFactor),
                Mathf.Clamp01(color.g * adjustmentFactor),
                Mathf.Clamp01(color.b * adjustmentFactor),
                color.a
            );
            
            return adjustedColor;
        }
        
        private Color ApplyV2BalanceAdjustment(Color color)
        {
            // V2: Radius-based color selection
            float r = color.r * 255f;
            float g = color.g * 255f;
            float b = color.b * 255f;
            
            // Calculate distance from center (128, 128, 128)
            float distance = Mathf.Sqrt(
                Mathf.Pow(r - 128f, 2) + 
                Mathf.Pow(g - 128f, 2) + 
                Mathf.Pow(b - 128f, 2)
            );
            
            bool withinRadius = distance <= v2Radius;
            bool shouldProcess = v2IncludeOutside ? true : withinRadius;
            
            if (shouldProcess)
            {
                float adjustmentFactor = withinRadius ? 
                    Mathf.Lerp(v2MinimumValue, 1f, (v2Radius - distance) / v2Radius) * v2Weight :
                    v2MinimumValue * v2Weight;
                    
                return new Color(
                    Mathf.Clamp01(color.r * adjustmentFactor),
                    Mathf.Clamp01(color.g * adjustmentFactor),
                    Mathf.Clamp01(color.b * adjustmentFactor),
                    color.a
                );
            }
            
            return color;
        }
        
        private Color ApplyV3BalanceAdjustment(Color color)
        {
            // V3: Grayscale calculation with gradient transformation
            // Human visual perception weights
            float grayscale = 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
            float grayscaleValue = grayscale * 255f;
            
            // Check if within gradient range
            if (grayscaleValue >= v3GradientStart && grayscaleValue <= v3GradientEnd)
            {
                float t = (grayscaleValue - v3GradientStart) / (v3GradientEnd - v3GradientStart);
                
                // Interpolate between original color and gradient color
                Color adjustedColor = Color.Lerp(color, v3GradientColor, t);
                return new Color(adjustedColor.r, adjustedColor.g, adjustedColor.b, color.a);
            }
            
            return color;
        }
        
        // Advanced color adjustment based on AdvancedColorConfiguration
        private Color ApplyAdvancedColorAdjustment(Color color)
        {
            Color result = color;
            
            // Brightness adjustment
            if (enableBrightness && !Mathf.Approximately(brightness, 1f))
            {
                result = new Color(
                    Mathf.Clamp01(result.r * brightness),
                    Mathf.Clamp01(result.g * brightness),
                    Mathf.Clamp01(result.b * brightness),
                    result.a
                );
            }
            
            // Contrast adjustment
            if (enableContrast && !Mathf.Approximately(contrast, 1f))
            {
                float midpoint = 0.5f;
                result = new Color(
                    Mathf.Clamp01((result.r - midpoint) * contrast + midpoint),
                    Mathf.Clamp01((result.g - midpoint) * contrast + midpoint),
                    Mathf.Clamp01((result.b - midpoint) * contrast + midpoint),
                    result.a
                );
            }
            
            // Gamma adjustment
            if (enableGamma && !Mathf.Approximately(gamma, 1f))
            {
                result = new Color(
                    Mathf.Clamp01(Mathf.Pow(result.r, 1f / gamma)),
                    Mathf.Clamp01(Mathf.Pow(result.g, 1f / gamma)),
                    Mathf.Clamp01(Mathf.Pow(result.b, 1f / gamma)),
                    result.a
                );
            }
            
            // Exposure adjustment
            if (enableExposure && !Mathf.Approximately(exposure, 0f))
            {
                float exposureFactor = Mathf.Pow(2f, exposure);
                result = new Color(
                    Mathf.Clamp01(result.r * exposureFactor),
                    Mathf.Clamp01(result.g * exposureFactor),
                    Mathf.Clamp01(result.b * exposureFactor),
                    result.a
                );
            }
            
            // Transparency adjustment
            if (enableTransparency && !Mathf.Approximately(transparency, 0f))
            {
                result = new Color(result.r, result.g, result.b, 
                    Mathf.Clamp01(result.a * (1f - transparency)));
            }
            
            return result;
        }
        
        
        // Helper method to update original texture to current state
        private void UpdateOriginalTexture()
        {
            Texture2D currentTexture = null;
            
            // Get current texture based on active tab
            if (activeTab == 0) // Basic tab
            {
                currentTexture = targetTexture;
            }
            else if (activeTab == 1) // Direct tab
            {
                currentTexture = GetExperimentalTargetTexture();
            }
            
            if (currentTexture != null)
            {
                // Clean up existing original texture
                if (originalTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(originalTexture, true);
                    originalTexture = null;
                }
                
                // Create new copy of current texture
                var readableTexture = TextureProcessor.MakeReadableCopy(currentTexture);
                if (readableTexture != null)
                {
                    originalTexture = readableTexture;
                }
            }
        }
        
        private void ClearPreview()
        {
            if (previewTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(previewTexture, true);
                previewTexture = null;
            }
            if (originalTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(originalTexture, true);
                originalTexture = null;
            }
            if (highPrecisionPreviewTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(highPrecisionPreviewTexture, true);
                highPrecisionPreviewTexture = null;
            }
            
            CancelDirectTabAutoPreview();
            DisposeReadableTextureCaches();
            lastAdjustmentIntensity = float.NaN;
            lastColorSelectionRange = -1f;
            lastUseHighPrecisionModeForPreview = !useHighPrecisionMode;
            directTabHasQueuedParameters = false;
            directTabPreviewPending = false;
            directTabPreviewInFlight = false;
            directTabQueuedParameterState = default;
            
            ClearUVMaskPreview();
        }
        
        // High-precision mode UI methods
        
        // High-precision mode for Direct tab only
        private void DrawHighPrecisionModeForDirectTab()
        {
            EditorGUILayout.LabelField("🎯 高精度モード (実験的機能)", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical("box");
            
            if (useHighPrecisionMode != lastDirectTabHighPrecisionState)
            {
                lastDirectTabHighPrecisionState = useHighPrecisionMode;
                ClearPreview();
                uvUsageStats = "";

                if (useHighPrecisionMode)
                {
                    GenerateUVMaskPreview();
                    if (referenceTexture != null && highPrecisionConfig != null && highPrecisionConfig.referenceGameObject != null)
                    {
                        if (highPrecisionPreviewTexture != null)
                        {
                            UnityEngine.Object.DestroyImmediate(highPrecisionPreviewTexture, true);
                        }
                        highPrecisionPreviewTexture = HighPrecisionProcessor.CreateHighPrecisionPreview(
                            referenceTexture, highPrecisionConfig, true);
                    }
                }
                else
                {
                    ClearUVMaskPreview();
                    if (highPrecisionPreviewTexture != null)
                    {
                        UnityEngine.Object.DestroyImmediate(highPrecisionPreviewTexture, true);
                        highPrecisionPreviewTexture = null;
                    }
                }
            }

            if (useHighPrecisionMode && uvMaskPreviewTexture != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("UV使用領域プレビュー", EditorStyles.centeredGreyMiniLabel);
                GUILayout.Label(uvMaskPreviewTexture, GUILayout.Width(128), GUILayout.Height(128));
                EditorGUILayout.LabelField("灰色部分: 未使用領域", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            
            if (useHighPrecisionMode)
            {
                EditorGUILayout.HelpBox("🎯 高精度モード: 「参考にしたい色」で選択されたGameObjectのメッシュが実際に使用しているUV領域のみを参照して、より正確な色合わせを実現します。", MessageType.Info);
                
                // Display currently used GameObject (from reference color selection)
                if (highPrecisionConfig.referenceGameObject != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("使用中のGameObject:", GUILayout.Width(150));
                    EditorGUILayout.ObjectField(highPrecisionConfig.referenceGameObject, typeof(GameObject), false, GUILayout.ExpandWidth(true));
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.HelpBox("高精度モードを使用するには、上の「参考にしたい色」でGameObjectを選択してください。", MessageType.Warning);
                }
                
                if (highPrecisionConfig.referenceGameObject != null)
                {
                    // Auto-configure based on reference material
                    AutoConfigureHighPrecisionSettings();
                    
                    // Show current configuration (read-only)
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField("高精度モード設定 (自動設定)", EditorStyles.boldLabel);
                    
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.LabelField($"マテリアルIndex: {highPrecisionConfig.materialIndex}");
                    EditorGUILayout.LabelField($"UVチャンネル: {highPrecisionConfig.uvChannel}");
                    EditorGUILayout.LabelField($"最小使用率しきい値: 1%");
                    EditorGUI.EndDisabledGroup();
                    
                    EditorGUILayout.EndVertical();
                    
                    // UV usage statistics for direct tab
                    if (!string.IsNullOrEmpty(uvUsageStats))
                    {
                        GUILayout.Space(5);
                        EditorGUILayout.LabelField("UV使用統計:", EditorStyles.boldLabel);
                        EditorGUILayout.TextArea(uvUsageStats, EditorStyles.helpBox, GUILayout.Height(60));
                    }
                    
                    // Update stats button
                    GUILayout.Space(5);
                    if (GUILayout.Button("📊 UV使用率を更新"))
                    {
                        UpdateUVUsageStatsForDirectTab();
                    }
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        // High-precision preview update for direct tab
        private void UpdateHighPrecisionPreviewForDirectTab()
        {
            if (!useHighPrecisionMode || referenceTexture == null || 
                !HighPrecisionProcessor.ValidateHighPrecisionConfig(highPrecisionConfig, referenceTexture))
                return;
            
            EditorApplication.delayCall += () => {
                if (highPrecisionPreviewTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(highPrecisionPreviewTexture, true);
                }
                
                highPrecisionPreviewTexture = HighPrecisionProcessor.CreateHighPrecisionPreview(
                    referenceTexture, highPrecisionConfig, true);
                
                UpdateUVUsageStatsForDirectTab();
                Repaint();
            };
        }
        
        private void UpdateUVUsageStatsForDirectTab()
        {
            if (!useHighPrecisionMode || referenceTexture == null || highPrecisionConfig.referenceGameObject == null)
            {
                uvUsageStats = "";
                return;
            }
            
            uvUsageStats = HighPrecisionProcessor.GetUVUsageStatistics(referenceTexture, highPrecisionConfig);
        }
        
        // Original experimental features for direct tab
        private void DrawExperimentalColorSelection()
        {
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
        }
        
        private void DrawHighPrecisionModeSection()
        {
            EditorGUILayout.LabelField("🎯 高精度モード", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical("box");
            
            // High-precision mode toggle
            bool newUseHighPrecision = EditorGUILayout.Toggle("高精度モードを使用", useHighPrecisionMode);
            if (newUseHighPrecision != useHighPrecisionMode)
            {
                useHighPrecisionMode = newUseHighPrecision;
                ClearPreview();
                uvUsageStats = "";
            }
            
            if (useHighPrecisionMode)
            {
                EditorGUILayout.HelpBox("🎯 高精度モード: 「参考にしたい色」で選択されたGameObjectのメッシュが実際に使用しているUV領域のみを参照して、より正確な色合わせを実現します。", MessageType.Info);
                
                // Display currently used GameObject (from reference color selection)
                if (highPrecisionConfig.referenceGameObject != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("使用中のGameObject:", GUILayout.Width(150));
                    EditorGUILayout.ObjectField(highPrecisionConfig.referenceGameObject, typeof(GameObject), false, GUILayout.ExpandWidth(true));
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.HelpBox("高精度モードを使用するには、上の「参考にしたい色」でGameObjectを選択してください。", MessageType.Warning);
                }
                
                if (highPrecisionConfig.referenceGameObject != null)
                {
                    // Auto-configure based on reference material
                    AutoConfigureHighPrecisionSettings();
                    
                    // Show current configuration (read-only)
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField("高精度モード設定 (自動設定)", EditorStyles.boldLabel);
                    
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.LabelField($"マテリアルIndex: {highPrecisionConfig.materialIndex}");
                    EditorGUILayout.LabelField($"UVチャンネル: {highPrecisionConfig.uvChannel}");
                    EditorGUILayout.LabelField($"最小使用率しきい値: 1%");
                    EditorGUI.EndDisabledGroup();
                    
                    EditorGUILayout.EndVertical();
                    
                    
                    // UV usage statistics
                    if (!string.IsNullOrEmpty(uvUsageStats))
                    {
                        GUILayout.Space(5);
                        EditorGUILayout.LabelField("UV使用統計:", EditorStyles.boldLabel);
                        EditorGUILayout.TextArea(uvUsageStats, EditorStyles.helpBox, GUILayout.Height(60));
                    }
                    
                    // Update stats button
                    GUILayout.Space(5);
                    if (GUILayout.Button("📊 UV使用率を更新"))
                    {
                        UpdateUVUsageStats();
                    }
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void UpdateHighPrecisionPreview()
        {
            if (!useHighPrecisionMode || referenceTexture == null || 
                !HighPrecisionProcessor.ValidateHighPrecisionConfig(highPrecisionConfig, referenceTexture))
                return;
            
            EditorApplication.delayCall += () => {
                if (highPrecisionPreviewTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(highPrecisionPreviewTexture, true);
                }
                
                highPrecisionPreviewTexture = HighPrecisionProcessor.CreateHighPrecisionPreview(
                    referenceTexture, highPrecisionConfig, true);
                
                UpdateUVUsageStats();
                Repaint();
            };
        }
        
        private void UpdateUVUsageStats()
        {
            if (!useHighPrecisionMode || referenceTexture == null || highPrecisionConfig.referenceGameObject == null)
            {
                uvUsageStats = "";
                return;
            }
            
            uvUsageStats = HighPrecisionProcessor.GetUVUsageStatistics(referenceTexture, highPrecisionConfig);
        }
        
        private void DrawHighPrecisionReferencePreview()
        {
            if (highPrecisionPreviewTexture == null) return;
            
            EditorGUILayout.LabelField("🎯 高精度モード - 参照テクスチャ(マスク表示)", EditorStyles.boldLabel);
            
            float maxSize = 300f;
            float aspectRatio = (float)highPrecisionPreviewTexture.width / highPrecisionPreviewTexture.height;
            float displayWidth = maxSize;
            float displayHeight = maxSize / aspectRatio;
            
            if (displayHeight > maxSize)
            {
                displayHeight = maxSize;
                displayWidth = maxSize * aspectRatio;
            }
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("メッシュ使用領域のみ表示:", EditorStyles.centeredGreyMiniLabel);
            GUILayout.Label(highPrecisionPreviewTexture, GUILayout.Width(displayWidth), GUILayout.Height(displayHeight));
            EditorGUILayout.LabelField("グレー部分は非使用領域", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        
        private void GenerateHighPrecisionPreview()
        {
            if (!CanProcessHighPrecision())
            {
                EditorUtility.DisplayDialog("エラー", "高精度モードの設定が不完全です。", "OK");
                return;
            }
            
            isProcessing = true;
            processingProgress = 0f;
            bool previewGenerated = false;
            
            try
            {
                // Always update original texture to reflect current state
                UpdateOriginalTexture();
                
                processingProgress = 0.3f;
                Repaint();
                
                previewTexture = HighPrecisionProcessor.ProcessWithHighPrecision(
                    targetTexture, referenceTexture, highPrecisionConfig, 
                    adjustmentIntensity, preserveLuminance, adjustmentMode);
                
                processingProgress = 1f;
                showPreview = true;
                previewGenerated = previewTexture != null;
                
                if (previewTexture == null)
                {
                    EditorUtility.DisplayDialog("エラー", "高精度プレビューの生成に失敗しました。テクスチャまたはマテリアル設定を確認してください。", "OK");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"High-precision preview generation failed: {e.Message}");
                EditorUtility.DisplayDialog("エラー", $"高精度プレビュー生成エラー: {e.Message}", "OK");
            }
            finally
            {
                isProcessing = false;
                Repaint();

                if (previewGenerated)
                {
                    UpdateParameterCache();
                    directTabHasQueuedParameters = false;
                }
            }
        }
        
        private void ApplyHighPrecisionAdjustment()
        {
            if (previewTexture == null)
            {
                EditorUtility.DisplayDialog("エラー", "適用するプレビューがありません。", "OK");
                return;
            }
            
            string originalPath = AssetDatabase.GetAssetPath(targetTexture);
            var saveOptions = SaveOptionsExtensions.GetDefaultVRChatSettings();
            
            int dialogResult = EditorUtility.DisplayDialogComplex(
                "高精度色変換を適用",
                "高精度モードでの変更を保存しますか？",
                "新しいファイルとして保存",
                "元ファイルを上書き",
                "キャンセル"
            );
            
            if (dialogResult == 2) return;
            
            saveOptions.overwriteOriginal = (dialogResult == 1);
            
            try
            {
                var fullResResult = HighPrecisionProcessor.ProcessWithHighPrecision(
                    targetTexture, referenceTexture, highPrecisionConfig,
                    adjustmentIntensity, preserveLuminance, adjustmentMode);
                
                if (fullResResult != null)
                {
                    if (TextureExporter.SaveTexture(fullResResult, originalPath, saveOptions))
                    {
                        EditorUtility.DisplayDialog("成功", "高精度色変換が正常に適用されました！", "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("エラー", "テクスチャの保存に失敗しました。", "OK");
                    }
                    
                    UnityEngine.Object.DestroyImmediate(fullResResult, true);
                }
                else
                {
                    EditorUtility.DisplayDialog("エラー", "高精度色変換処理に失敗しました。", "OK");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"High-precision adjustment application failed: {e.Message}");
                EditorUtility.DisplayDialog("エラー", $"高精度色変換エラー: {e.Message}", "OK");
            }
        }
        
        private bool CanProcessHighPrecision()
        {
            return CanProcess() && useHighPrecisionMode && 
                   HighPrecisionProcessor.ValidateHighPrecisionConfig(highPrecisionConfig, referenceTexture);
        }
        
        private void DrawShaderSettingsTab()
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
    
}

