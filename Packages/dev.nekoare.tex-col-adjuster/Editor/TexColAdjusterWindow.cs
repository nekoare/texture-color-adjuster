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
    public partial class TexColAdjusterWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private int activeTab = 1;
        private const string ActiveTabPrefKey = "TexColAdjuster_ActiveTab";
        
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
        // showPreview, realTimePreview, directTabAutoPreviewEnabled removed: always enabled
        private bool directTabPreviewPending = false;
        private bool directTabPreviewInFlight = false;
        private bool directTabHasQueuedParameters = false;
        private double directTabNextPreviewTime = 0d;
        private DirectPreviewParameterState directTabQueuedParameterState;
        private const double DirectTabPreviewDebounceSecondsGPU = 0.2d;
        private const double DirectTabPreviewDebounceSecondsCPU = 0.5d;
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
        private float gammaCorrection = 1f;
        private float midtoneShift = 0f;
        
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
        private HighPrecisionProcessor.HighPrecisionConfig highPrecisionConfig = new HighPrecisionProcessor.HighPrecisionConfig();
        private Texture2D highPrecisionPreviewTexture = null;
        private Texture2D uvMaskPreviewTexture = null;
        private string uvUsageStats = "";
        
        // NDMF integration
        private bool useNDMFDirectMode = true;

        // New UI toggle
        private bool useNewUI = true;
        private bool showAdvancedSettingsFoldout = false;
        private bool showNDMFSettingsFoldout = false;
        private const string EditorPrefKey_UseNewUI = "TexColAdjuster_UseNewUI";
        
        // Shader transfer new UI fields
        private UnityEngine.Object shaderTransferNewSource = null;
        private Material shaderTransferNewSourceMaterial = null;
        private List<Material> shaderTransferNewTargetMaterials = new List<Material>();
        private HashSet<Material> shaderTransferNewTargetSelected = new HashSet<Material>();
        private MaterialUnifyToolMethods.TransferCategories shaderTransferNewCategories =
            MaterialUnifyToolMethods.TransferCategories.ライティング設定 |
            MaterialUnifyToolMethods.TransferCategories.影 |
            MaterialUnifyToolMethods.TransferCategories.リムシェード |
            MaterialUnifyToolMethods.TransferCategories.逆光ライト |
            MaterialUnifyToolMethods.TransferCategories.輪郭線;
        private bool showShaderTransferTexOptions = false;

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
        private float lastHueShift = float.NaN;
        private float lastSaturationMultiplier = float.NaN;
        private float lastBrightnessOffset = float.NaN;
        private float lastContrastMultiplier = float.NaN;
        private float lastGammaCorrection = float.NaN;
        private float lastMidtoneShift = float.NaN;

        // Cached LAB matching result for fast post-adjustment updates
        private RenderTexture _cachedLabMatchRT = null;

        // Cached uncompressed textures to avoid block noise from compressed formats
        private Texture2D _cachedUncompressedTarget = null;
        private Texture2D _cachedUncompressedRef = null;
        private Texture2D _cachedUncompressedTargetSource = null; // original texture reference for cache invalidation
        private Texture2D _cachedUncompressedRefSource = null;
        private TextureImportBackup _targetImportBackup = null;
        private TextureImportBackup _refImportBackup = null;
        
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
        private Component balanceRendererComponent;
        private Material balanceSelectedMaterial;
        private Color previousColor = Color.white; // Source color in ColorChanger
        private Color newColor = Color.white; // Target color in ColorChanger
        private bool balanceModeEnabled = true;
        // transparentModeEnabled removed: alpha is always preserved automatically
        private enum BalanceModeVersion { V1_Distance, V2_Radius, V3_Gradient }
        private BalanceModeVersion balanceModeVersion = BalanceModeVersion.V1_Distance;
        
        // Scene preview: temporarily replace material on renderer for in-scene preview
        private Renderer scenePreviewRenderer;
        private int scenePreviewMaterialSlot;
        private Material scenePreviewOriginalMaterial;
        private Material scenePreviewClonedMaterial;
        private bool scenePreviewActive;
        private Texture scenePreviewRenderTexture; // GPU result (RenderTexture) kept alive for Scene display
        private bool showWindowPreview = false; // Toggle for in-window preview (GPU fallback)

        // Texture preview for color picking
        private Vector2 texturePreviewScrollPosition;
        
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
            window.minSize = new Vector2(800, 500);
            window.Show();
        }
        
        private void OnEnable()
        {
            LoadPresets();
            useNDMFDirectMode = NDMFIntegrationHelper.GetNDMFMode("Direct");
            useNewUI = EditorPrefs.GetBool(EditorPrefKey_UseNewUI, true);
            showWindowPreview = EditorPrefs.GetBool("TexColAdjuster_ShowWindowPreview", false);
            activeTab = EditorPrefs.GetInt(ActiveTabPrefKey, 1);
        }
        
        private void OnDisable()
        {
            RestoreScenePreview();
            DisposeScenePreviewRenderTexture();
            CancelDirectTabAutoPreview();
            DisposeReadableTextureCaches();
            RestoreUncompressedTextureCache();
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
            
            // Handle auto preview
            if (Event.current.type == EventType.Repaint)
            {
                if (activeTab == 0 && useNewUI && CanProcess() && (previewTexture == null || HasParametersChanged()))
                {
                    GeneratePreview();
                    UpdateParameterCache();
                }
                else if (activeTab == 0 && !useNewUI && CanProcessSmartColorMatch() && HasSmartColorMatchParametersChanged())
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
        
        private void DrawHeader()
        {
            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(LocalizationManager.GetLanguageDisplayName(), GUILayout.Width(80)))
            {
                LocalizationManager.ToggleLanguage();
                Repaint();
            }

            GUILayout.Space(10);
            bool newUseNewUI = EditorGUILayout.ToggleLeft(LocalizationManager.Get("new_ui_toggle"), useNewUI, GUILayout.Width(60));
            if (newUseNewUI != useNewUI)
            {
                useNewUI = newUseNewUI;
                EditorPrefs.SetBool(EditorPrefKey_UseNewUI, useNewUI);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(LocalizationManager.Get("reset_inputs"), GUILayout.Width(120)))
            {
                ResetInputsToInitialState();
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
            int newTab = GUILayout.Toolbar(activeTab, localizedTabs);
            if (newTab != activeTab)
            {
                ResetInputsToInitialState();
                activeTab = newTab;
                EditorPrefs.SetInt(ActiveTabPrefKey, activeTab);
            }
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


        // Advanced tab (replaces old Color Adjust tab)
        

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
        

        private void ResetInputsToInitialState()
        {
            // Reset user-adjustable inputs back to the initial window state.
            ClearPreview();
            DisposeReadableTextureCaches();
            RestoreUncompressedTextureCache();
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
            highPrecisionConfig = new HighPrecisionProcessor.HighPrecisionConfig();
            uvUsageStats = "";

            useNDMFDirectMode = NDMFIntegrationHelper.GetNDMFMode("Direct");

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

            shaderTransferNewSource = null;
            shaderTransferNewSourceMaterial = null;
            shaderTransferNewTargetMaterials.Clear();
            shaderTransferNewTargetSelected.Clear();
            showShaderTransferTexOptions = false;
            shaderTransferNewCategories =
                MaterialUnifyToolMethods.TransferCategories.ライティング設定 |
                MaterialUnifyToolMethods.TransferCategories.影 |
                MaterialUnifyToolMethods.TransferCategories.リムシェード |
                MaterialUnifyToolMethods.TransferCategories.逆光ライト |
                MaterialUnifyToolMethods.TransferCategories.輪郭線;

            referenceGameObject = null;
            targetGameObject = null;
            referenceComponent = null;
            targetComponent = null;
            selectedReferenceMaterial = null;
            selectedTargetMaterial = null;

            enableMaterialTransfer = false;
            materialTransferDirection = 0;

            balanceRendererComponent = null;
            balanceSelectedMaterial = null;
            previousColor = Color.white;
            newColor = Color.white;
            balanceModeEnabled = true;
            // transparentModeEnabled removed
            balanceModeVersion = BalanceModeVersion.V1_Distance;

            texturePreviewScrollPosition = Vector2.zero;
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
        
        
        private Material[] ExtractMaterials(Component renderer)
        {
            Material[] materials = null;
            if (renderer is SkinnedMeshRenderer smr)
                materials = smr.sharedMaterials;
            else if (renderer is MeshRenderer mr)
                materials = mr.sharedMaterials;

            if (materials == null)
                return null;

            // If scene preview is active on this renderer, substitute the cloned material back to the original
            if (scenePreviewActive && scenePreviewRenderer == (renderer as Renderer)
                && scenePreviewClonedMaterial != null && scenePreviewOriginalMaterial != null
                && scenePreviewMaterialSlot >= 0 && scenePreviewMaterialSlot < materials.Length)
            {
                materials = (Material[])materials.Clone();
                materials[scenePreviewMaterialSlot] = scenePreviewOriginalMaterial;
            }

            return materials;
        }

        private bool IsLiltoonMaterial(Material material)
        {
            return MaterialUnifyToolMethods.IsLiltoonMaterial(material);
        }
        
        private Texture2D GetMainTexture(Material material)
        {
            // If Scene preview is active on this material, return the original texture
            // (the material's _MainTex may be swapped to a preview texture via cloned material on renderer)
            if (scenePreviewActive && scenePreviewOriginalMaterial == material)
                return scenePreviewOriginalMaterial.GetTexture("_MainTex") as Texture2D;

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
        private void ApplyColorChangerAdjustment()
        {
            ApplySmartColorMatchAdjustment();
        }
        
        private bool CanProcessColorChanger()
        {
            return CanProcessSmartColorMatch();
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
            RestoreUncompressedTextureCache();
        }
        
        // Color Adjust (ColorChanger Style) helper methods
        
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
        private Renderer ResolveRendererForMaterial(Material material)
        {
            if (material == null) return null;

            // Direct tab
            if (targetComponent is Renderer targetRenderer)
            {
                var mats = targetRenderer.sharedMaterials;
                if (mats != null && System.Array.IndexOf(mats, material) >= 0)
                    return targetRenderer;
            }

            // ColorAdjust tab
            if (balanceRendererComponent is Renderer balanceRenderer)
            {
                var mats = balanceRenderer.sharedMaterials;
                if (mats != null && System.Array.IndexOf(mats, material) >= 0)
                    return balanceRenderer;
            }

            return null;
        }

        private int FindMaterialSlot(Renderer renderer, Material material)
        {
            if (renderer == null || material == null) return -1;
            var materials = renderer.sharedMaterials;
            if (materials == null) return -1;
            return System.Array.IndexOf(materials, material);
        }
        private bool CanProcessHighPrecision()
        {
            return CanProcess() && useHighPrecisionMode && 
                   HighPrecisionProcessor.ValidateHighPrecisionConfig(highPrecisionConfig, referenceTexture);
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

