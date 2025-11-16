using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if VRC_SDK_VRCSDK3
using VRC.SDKBase;
#endif

namespace TexColAdjuster.Runtime
{
    [AddComponentMenu("TexColorAdjuster/Texture Color Adjustment")]
    public class TextureColorAdjustmentComponent : MonoBehaviour
#if VRC_SDK_VRCSDK3
        , IEditorOnly
#endif
        , ISerializationCallbackReceiver
    {
        [Serializable]
        public struct TargetBinding
        {
            public Renderer renderer;
            public int materialSlot;

            public bool IsValid => renderer != null && materialSlot >= 0;
        }

        [Header("Target Settings")]
        [SerializeField]
        private List<TargetBinding> targetBindings = new List<TargetBinding>();

        [SerializeField, HideInInspector, FormerlySerializedAs("targetRenderer")]
        private Renderer legacyTargetRenderer;

        [SerializeField, HideInInspector, FormerlySerializedAs("materialSlot")]
        private int legacyMaterialSlot = 0;

        [SerializeField, HideInInspector]
        private int bindingsVersion;

        [SerializeField, HideInInspector]
        private int bindingsHash;

        public IReadOnlyList<TargetBinding> TargetBindings => targetBindings;
        public int BindingVersion => bindingsVersion;

        public bool HasValidBindings
        {
            get
            {
                if (targetBindings == null)
                    return false;

                foreach (var binding in targetBindings)
                {
                    if (binding.IsValid)
                        return true;
                }

                return false;
            }
        }

        [Header("Reference Texture")]
        [Tooltip("Reference texture to match colors from")]
        public Texture2D referenceTexture;

        [Header("Adjustment Settings")]
        [Tooltip("Color adjustment algorithm to use")]
        public ColorAdjustmentMode adjustmentMode = ColorAdjustmentMode.LabHistogramMatching;

        [Range(0f, 1f)]
        [Tooltip("Intensity of the color adjustment (0 = no change, 1 = full adjustment)")]
        public float intensity = 1.0f;

        [Tooltip("Preserve the original luminance values while adjusting color")]
        public bool preserveLuminance = false;

        [Header("Dual Color Selection")]
        [Tooltip("Use dual color selection mode for precise color matching")]
        public bool useDualColorSelection = false;

        [Tooltip("Target color to adjust in the texture")]
        public Color targetColor = Color.white;

        [Tooltip("Reference color to match from the reference texture")]
        public Color referenceColor = Color.white;

        [Range(0f, 1f)]
        [Tooltip("Range for color selection in dual color mode")]
        public float selectionRange = 0.3f;

        [Header("Processing Settings")]
        [FormerlySerializedAs("Enabled")]
        [Tooltip("Apply this component during build")]
        public bool applyDuringBuild = true;

        public bool Enabled
        {
            get => applyDuringBuild;
            set => applyDuringBuild = value;
        }

        [Header("Preview Settings")]
        [Tooltip("Enable real-time preview in Scene View")]
        public bool PreviewEnabled = true;

        [Tooltip("Use CPU processing for preview (slower but more compatible). Uncheck to use GPU processing (faster).")]
        public bool PreviewOnCPU = false;

    [Header("Post-Adjustment Settings")]
    [Tooltip("Hue shift in degrees (-180..180). Positive shifts hue clockwise.")]
    [Range(-180f, 180f)]
    public float hueShift = 0f;

    [Tooltip("Saturation multiplier (1 = no change)")]
    [Range(0f, 3f)]
    public float saturation = 1f;

    [Tooltip("Brightness multiplier (1 = no change)")]
    [Range(0f, 3f)]
    public float brightness = 1f;

    [Tooltip("Gamma correction (1 = no change). Values <1 make the image lighter; >1 make it darker.")]
    [Range(0.1f, 5f)]
    public float gamma = 1f;

        [Header("High Precision Mode")]
        [Tooltip("Use high precision mode for mesh UV-aware color adjustment")]
        public bool useHighPrecisionMode = false;

        [Tooltip("Reference GameObject for UV analysis")]
        public GameObject highPrecisionReferenceObject;

        [Tooltip("Material index to analyze")]
        public int highPrecisionMaterialIndex = 0;

        [Range(0, 3)]
        [Tooltip("UV channel to use (0-3)")]
        public int highPrecisionUVChannel = 0;

        [Range(3, 10)]
        [Tooltip("Number of dominant colors to extract")]
        public int highPrecisionDominantColorCount = 5;

        [Tooltip("Use weighted sampling for color distribution")]
        public bool highPrecisionUseWeightedSampling = true;

        [Tooltip("Precomputed UV mask used to limit adjustments to the mesh area")]
        public Texture2D highPrecisionMaskTexture;

        [Range(0f, 1f)]
        [Tooltip("Threshold applied to the mask texture when determining used pixels")]
        public float highPrecisionMaskThreshold = 0.5f;

        public void SetSingleBinding(Renderer renderer, int materialSlot, bool validateRendererSlots = true)
        {
            if (targetBindings == null)
            {
                targetBindings = new List<TargetBinding>();
            }

            targetBindings.Clear();

            if (renderer != null)
            {
                targetBindings.Add(new TargetBinding
                {
                    renderer = renderer,
                    materialSlot = materialSlot
                });
            }

            SanitizeBindings(validateRendererSlots);
        }

        public void SetBindings(IEnumerable<TargetBinding> bindings, bool validateRendererSlots = true)
        {
            if (targetBindings == null)
            {
                targetBindings = new List<TargetBinding>();
            }

            targetBindings.Clear();

            if (bindings != null)
            {
                foreach (var binding in bindings)
                {
                    if (!binding.IsValid)
                        continue;

                    targetBindings.Add(binding);
                }
            }

            SanitizeBindings(validateRendererSlots);
        }

        public void AddBinding(Renderer renderer, int materialSlot, bool validateRendererSlots = true)
        {
            if (renderer == null)
                return;

            if (targetBindings == null)
            {
                targetBindings = new List<TargetBinding>();
            }

            targetBindings.Add(new TargetBinding
            {
                renderer = renderer,
                materialSlot = materialSlot
            });

            SanitizeBindings(validateRendererSlots);
        }

        public IEnumerable<TargetBinding> EnumerateValidBindings()
        {
            if (targetBindings == null)
                yield break;

            foreach (var binding in targetBindings)
            {
                if (binding.IsValid)
                    yield return binding;
            }
        }

        public TargetBinding GetBinding(int index)
        {
            if (targetBindings == null || index < 0 || index >= targetBindings.Count)
                return default;

            return targetBindings[index];
        }

        public Renderer GetPrimaryRenderer()
        {
            if (targetBindings == null || targetBindings.Count == 0)
                return null;

            return targetBindings[0].renderer;
        }

        public int GetPrimaryMaterialSlot()
        {
            if (targetBindings == null || targetBindings.Count == 0)
                return 0;

            return Mathf.Max(0, targetBindings[0].materialSlot);
        }

        public void NotifyBindingsChanged(bool validateRendererSlots = true)
        {
            SanitizeBindings(validateRendererSlots);
        }

        private void OnValidate()
        {
            EnsureLegacyBindingMigrated();
            SanitizeBindings();
        }

        private void EnsureLegacyBindingMigrated(bool validateRendererSlots = true)
        {
            if ((targetBindings == null || targetBindings.Count == 0) && legacyTargetRenderer != null)
            {
                SetSingleBinding(legacyTargetRenderer, legacyMaterialSlot, validateRendererSlots);
                legacyTargetRenderer = null;
            }

            if (targetBindings == null)
            {
                targetBindings = new List<TargetBinding>();
            }
        }

        private void SanitizeBindings(bool validateRendererSlots = true)
        {
            if (targetBindings == null)
            {
                targetBindings = new List<TargetBinding>();
            }

            bool modified = false;

            for (int i = targetBindings.Count - 1; i >= 0; i--)
            {
                var binding = targetBindings[i];

                if (binding.renderer == null)
                {
                    targetBindings.RemoveAt(i);
                    modified = true;
                    continue;
                }

                int clampedSlot;

                if (validateRendererSlots)
                {
                    var materials = binding.renderer.sharedMaterials;
                    int maxSlot = materials != null ? materials.Length - 1 : -1;
                    clampedSlot = Mathf.Clamp(binding.materialSlot, 0, Mathf.Max(0, maxSlot));
                }
                else
                {
                    clampedSlot = Mathf.Max(0, binding.materialSlot);
                }

                if (clampedSlot != binding.materialSlot)
                {
                    binding.materialSlot = clampedSlot;
                    targetBindings[i] = binding;
                    modified = true;
                }
            }

            int previousHash = bindingsHash;
            int newHash = ComputeBindingsHash();

            if (modified || newHash != previousHash)
            {
                bindingsHash = newHash;
                bindingsVersion++;
            }
        }

        private int ComputeBindingsHash()
        {
            unchecked
            {
                int hash = 17;
                if (targetBindings != null)
                {
                    for (int i = 0; i < targetBindings.Count; i++)
                    {
                        var binding = targetBindings[i];
                        hash = hash * 23 + (binding.renderer ? binding.renderer.GetInstanceID() : 0);
                        hash = hash * 23 + binding.materialSlot;
                    }
                }
                return hash;
            }
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            EnsureLegacyBindingMigrated(false);
            SanitizeBindings(false);
        }
    }
}
