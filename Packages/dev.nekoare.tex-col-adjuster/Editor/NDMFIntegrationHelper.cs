using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TexColAdjuster;
using TexColAdjuster.Runtime;

namespace TexColAdjuster.Editor
{
    /// <summary>
    /// Helper class for integrating NDMF functionality into the existing EditorWindow
    /// </summary>
    public static class NDMFIntegrationHelper
    {
        private const string MaskRootFolderName = "TexColAdjuster_Generated";
        private const string MaskRootFolderPath = "Assets/" + MaskRootFolderName;
        private const string MaskSubFolderName = "Masks";

        /// <summary>
        /// Get NDMF mode preference for a specific tab
        /// </summary>
        public static bool GetNDMFMode(string tabName)
        {
            string key = $"TexColAdjuster_NDMFMode_{tabName}";
            return EditorPrefs.GetBool(key, true); // Default: true (NDMF mode ON)
        }

        /// <summary>
        /// Set NDMF mode preference for a specific tab
        /// </summary>
        public static void SetNDMFMode(string tabName, bool value)
        {
            string key = $"TexColAdjuster_NDMFMode_{tabName}";
            EditorPrefs.SetBool(key, value);
        }

        /// <summary>
        /// Find all renderers in the scene that use the specified texture
        /// </summary>
        public static Renderer[] FindRenderersUsingTexture(Texture2D texture)
        {
            if (texture == null)
                return new Renderer[0];

            var allRenderers = Object.FindObjectsOfType<Renderer>();
            var matchingRenderers = new List<Renderer>();

            foreach (var renderer in allRenderers)
            {
                var materials = renderer.sharedMaterials;
                foreach (var mat in materials)
                {
                    if (mat == null)
                        continue;

                    if (MaterialHasTexture(mat, texture))
                    {
                        matchingRenderers.Add(renderer);
                        break;
                    }
                }
            }

            return matchingRenderers.ToArray();
        }

        /// <summary>
        /// Check if a material uses the specified texture
        /// </summary>
        public static bool MaterialHasTexture(Material material, Texture2D texture)
        {
            if (material == null || texture == null)
                return false;

            var shader = material.shader;
            int propertyCount = shader.GetPropertyCount();

            for (int i = 0; i < propertyCount; i++)
            {
                if (shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                {
                    string propName = shader.GetPropertyName(i);
                    var tex = material.GetTexture(propName);

                    if (tex == texture)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Find the material slot index that contains the specified texture
        /// </summary>
        public static int FindMaterialSlotWithTexture(Renderer renderer, Texture2D texture)
        {
            if (renderer == null || texture == null)
                return 0;

            var materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null)
                    continue;

                if (MaterialHasTexture(mat, texture))
                    return i;
            }

            return 0;
        }

        /// <summary>
        /// Find the material slot index that contains the specified material reference
        /// </summary>
        public static int FindMaterialSlotWithMaterial(Renderer renderer, Material material)
        {
            if (renderer == null || material == null)
                return 0;

            var materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null)
                    continue;

                if (mat == material)
                    return i;
            }

            return 0;
        }

        /// <summary>
        /// Add or update TextureColorAdjustmentComponent on the renderer's GameObject
        /// </summary>
        public static TextureColorAdjustmentComponent AddOrUpdateComponent(
            Renderer targetRenderer,
            int materialSlot,
            Texture2D referenceTexture,
            ColorAdjustmentMode adjustmentMode,
            float intensity,
            bool preserveLuminance,
            bool useDualColorSelection,
            Color targetColor,
            Color referenceColor,
            float selectionRange,
            // Post-adjustments
            float hueShift = 0f,
            float saturation = 1f,
            float brightness = 1f,
            float gamma = 1f,
            bool useHighPrecisionMode = false,
            GameObject highPrecisionReferenceObject = null,
            int highPrecisionMaterialIndex = 0,
            int highPrecisionUVChannel = 0,
            int highPrecisionDominantColorCount = 5,
            bool highPrecisionUseWeightedSampling = true)
        {
            if (targetRenderer == null)
                return null;

            // Find existing component for the same material slot
            var existingComponents = targetRenderer.GetComponents<TextureColorAdjustmentComponent>();
            TextureColorAdjustmentComponent component = existingComponents
                .FirstOrDefault(comp => comp.EnumerateValidBindings()
                    .Any(binding => binding.renderer == targetRenderer && binding.materialSlot == materialSlot));

            // Create or update component
            if (component == null)
            {
                component = Undo.AddComponent<TextureColorAdjustmentComponent>(targetRenderer.gameObject);
                component.SetSingleBinding(targetRenderer, materialSlot);
                Debug.Log($"[TexColorAdjuster] Added component to '{targetRenderer.gameObject.name}'");
            }
            else
            {
                Undo.RecordObject(component, "Update Texture Color Adjustment");
                component.SetSingleBinding(targetRenderer, materialSlot);
                Debug.Log($"[TexColAdjuster] Updated existing component on '{targetRenderer.gameObject.name}'");
            }

            // Apply settings
            component.referenceTexture = referenceTexture;
            component.adjustmentMode = adjustmentMode;
            component.intensity = intensity / 100f; // Convert from 0-100 to 0-1
            component.preserveLuminance = preserveLuminance;
            component.useDualColorSelection = useDualColorSelection;
            component.targetColor = targetColor;
            component.referenceColor = referenceColor;
            component.selectionRange = selectionRange;

            // Apply post-adjustments
            component.hueShift = hueShift;
            component.saturation = saturation;
            component.brightness = brightness;
            component.gamma = gamma;

            // Apply high precision mode settings
            component.useHighPrecisionMode = useHighPrecisionMode;
            component.highPrecisionReferenceObject = highPrecisionReferenceObject;
            component.highPrecisionMaterialIndex = highPrecisionMaterialIndex;
            component.highPrecisionUVChannel = highPrecisionUVChannel;
            component.highPrecisionDominantColorCount = highPrecisionDominantColorCount;
            component.highPrecisionUseWeightedSampling = highPrecisionUseWeightedSampling;

            // Enable preview
            component.PreviewEnabled = true;
            component.applyDuringBuild = true;

            EditorUtility.SetDirty(component);

            return component;
        }

        /// <summary>
        /// Apply NDMF settings from EditorWindow parameters
        /// </summary>
        public static void ApplyNDMFSettings(
            Texture2D targetTexture,
            Texture2D referenceTexture,
            ColorAdjustmentMode adjustmentMode,
            float intensity,
            bool preserveLuminance,
            bool useDualColorSelection,
            Color targetColor,
            Color referenceColor,
            float selectionRange,
            // Post-adjustments
            float hueShift = 0f,
            float saturation = 1f,
            float brightness = 1f,
            float gamma = 1f,
            bool useHighPrecisionMode = false,
            GameObject highPrecisionReferenceObject = null,
            int highPrecisionMaterialIndex = 0,
            int highPrecisionUVChannel = 0,
            int highPrecisionDominantColorCount = 5,
            bool highPrecisionUseWeightedSampling = true)
        {
            if (targetTexture == null || referenceTexture == null)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    "Both target texture and reference texture must be assigned.",
                    "OK"
                );
                return;
            }

            // Find renderers using the target texture
            var renderers = FindRenderersUsingTexture(targetTexture);

            if (renderers.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Renderer Found",
                    $"No renderer is using the texture '{targetTexture.name}' in the current scene.",
                    "OK"
                );
                return;
            }

            Renderer selectedRenderer;
            int materialSlot;

            if (renderers.Length == 1)
            {
                // Single renderer found
                selectedRenderer = renderers[0];
                materialSlot = FindMaterialSlotWithTexture(selectedRenderer, targetTexture);
            }
            else
            {
                // Multiple renderers found - show selection dialog
                var selection = ShowRendererSelectionWindow(renderers, targetTexture);
                if (selection.renderer == null)
                    return; // User cancelled

                selectedRenderer = selection.renderer;
                materialSlot = selection.materialSlot;
            }

            // Add or update component
            var component = AddOrUpdateComponent(
                selectedRenderer,
                materialSlot,
                referenceTexture,
                adjustmentMode,
                intensity,
                preserveLuminance,
                useDualColorSelection,
                targetColor,
                referenceColor,
                selectionRange,
                hueShift,
                saturation,
                brightness,
                gamma,
                useHighPrecisionMode,
                highPrecisionReferenceObject,
                highPrecisionMaterialIndex,
                highPrecisionUVChannel,
                highPrecisionDominantColorCount,
                highPrecisionUseWeightedSampling
            );

            if (component != null)
            {
                EditorUtility.DisplayDialog(
                    "Success",
                    $"Settings applied to '{selectedRenderer.gameObject.name}'.\n" +
                    $"Material Slot: {materialSlot}\n" +
                    "Preview is now active in Scene View.",
                    "OK"
                );

                // Select the component in the Inspector
                Selection.activeObject = component;
                EditorGUIUtility.PingObject(component);

                // Repaint Scene View
                SceneView.RepaintAll();
            }
        }

        private static (Renderer renderer, int materialSlot) ShowRendererSelectionWindow(Renderer[] renderers, Texture2D texture)
        {
            // For now, use a simple dialog with indices
            // In a production implementation, this would be a custom EditorWindow
            string[] options = renderers.Select((r, i) =>
            {
                int slot = FindMaterialSlotWithTexture(r, texture);
                return $"{i}: {r.gameObject.name} (Slot {slot})";
            }).ToArray();

            // Note: GenericMenu would be better for user experience
            // This is a simplified implementation
            if (renderers.Length > 0)
            {
                // Automatically select the first one for now
                // TODO: Implement proper selection dialog
                int selectedIndex = 0;
                return (renderers[selectedIndex], FindMaterialSlotWithTexture(renderers[selectedIndex], texture));
            }

            return (null, 0);
        }

        /// <summary>
        /// Add TextureColorAdjustmentComponent instances as child GameObjects under the avatar root.
        /// This keeps components centralized (under a single container) instead of attaching them to each mesh.
        /// Returns the number of components added.
        /// </summary>
        public static int AddNDMFComponentsUnderAvatarRoot(
            GameObject targetGameObject,
            Material targetMaterial,
            Texture2D referenceTexture,
            ColorAdjustmentMode adjustmentMode,
            float intensity,
            bool preserveLuminance,
            bool useDualColorSelection,
            Color targetColor,
            Color referenceColor,
            float selectionRange,
            // Post-adjustments
            float hueShift = 0f,
            float saturation = 1f,
            float brightness = 1f,
            float gamma = 1f,
            bool useHighPrecisionMode = false,
            GameObject highPrecisionReferenceObject = null,
            int highPrecisionMaterialIndex = 0,
            int highPrecisionUVChannel = 0,
            int highPrecisionDominantColorCount = 5,
            bool highPrecisionUseWeightedSampling = true,
            bool includeInactive = false)
        {
            if (targetGameObject == null || targetMaterial == null || referenceTexture == null)
            {
                EditorUtility.DisplayDialog("エラー", "ターゲットGameObject / マテリアル / 参照テクスチャが必要です。", "OK");
                return 0;
            }

            // Determine root (avatar root)
            var root = targetGameObject.transform.root.gameObject;

            // Find or create a container under root to hold generated GameObjects
            string containerName = "TexColAdjuster_NDMF";
            Transform containerTransform = root.transform.Find(containerName);
            GameObject container;
            if (containerTransform == null)
            {
                container = new GameObject(containerName);
                Undo.RegisterCreatedObjectUndo(container, "Create NDMF container");
                container.transform.SetParent(root.transform, false);
            }
            else
            {
                container = containerTransform.gameObject;
            }

            var hostObject = EnsureMaterialHost(container.transform, targetMaterial);
            if (hostObject == null)
            {
                Debug.LogError("[TexColAdjuster] Failed to prepare material host for NDMF components.");
                return 0;
            }

            var component = hostObject.GetComponent<TextureColorAdjustmentComponent>();
            bool createdNewComponent = false;
            if (component == null)
            {
                component = Undo.AddComponent<TextureColorAdjustmentComponent>(hostObject);
                createdNewComponent = true;
            }
            else
            {
                Undo.RecordObject(component, "Update NDMF component");
            }

            var bindings = new List<TextureColorAdjustmentComponent.TargetBinding>();
            var bindingSet = new HashSet<(Renderer renderer, int slot)>();

            // Find all renderers under root
            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive);

            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                var materials = renderer.sharedMaterials;
                if (materials == null) continue;

                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var mat = materials[slot];
                    if (mat == null || mat != targetMaterial)
                        continue;

                    var mainTexture = mat.GetTexture("_MainTex") as Texture2D;
                    if (mainTexture == null)
                        continue;

                    if (!bindingSet.Add((renderer, slot)))
                        continue;

                    bindings.Add(new TextureColorAdjustmentComponent.TargetBinding
                    {
                        renderer = renderer,
                        materialSlot = slot
                    });
                }
            }

            if (bindings.Count == 0)
            {
                component.SetBindings(null);
                EditorUtility.DisplayDialog("情報", "対象マテリアルを使用するレンダラーは見つかりませんでした。", "OK");
                return 0;
            }

            component.SetBindings(bindings);

            component.referenceTexture = referenceTexture;
            component.adjustmentMode = adjustmentMode;
            component.intensity = intensity / 100f;
            component.preserveLuminance = preserveLuminance;
            component.useDualColorSelection = useDualColorSelection;
            component.targetColor = targetColor;
            component.referenceColor = referenceColor;
            component.selectionRange = selectionRange;

            component.hueShift = hueShift;
            component.saturation = saturation;
            component.brightness = brightness;
            component.gamma = gamma;

            component.useHighPrecisionMode = false;
            component.highPrecisionReferenceObject = null;
            component.highPrecisionMaterialIndex = 0;
            component.highPrecisionUVChannel = 0;
            component.highPrecisionDominantColorCount = highPrecisionDominantColorCount;
            component.highPrecisionUseWeightedSampling = highPrecisionUseWeightedSampling;
            component.highPrecisionMaskTexture = null;
            component.highPrecisionMaskThreshold = 0.5f;

            component.PreviewEnabled = true;
            component.applyDuringBuild = true;
            EditorUtility.SetDirty(component);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);

            string message = createdNewComponent
                ? $"対象マテリアルを使うレンダラー{bindings.Count}件に対応するコンポーネントを追加しました。\nコンテナ: {containerName}"
                : $"対象マテリアルを使うレンダラー{bindings.Count}件に対応するコンポーネントを更新しました。\nコンテナ: {containerName}";
            EditorUtility.DisplayDialog("完了", message, "OK");

            return bindings.Count;
        }

        private static string EnsureMaskFolder(GameObject root)
        {
            if (!AssetDatabase.IsValidFolder(MaskRootFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", MaskRootFolderName);
            }

            string maskFolderPath = MaskRootFolderPath + "/" + MaskSubFolderName;
            if (!AssetDatabase.IsValidFolder(maskFolderPath))
            {
                AssetDatabase.CreateFolder(MaskRootFolderPath, MaskSubFolderName);
            }

            string avatarFolderName = SanitizePathName(root?.name);
            string avatarFolderPath = maskFolderPath + "/" + avatarFolderName;
            if (!AssetDatabase.IsValidFolder(avatarFolderPath))
            {
                AssetDatabase.CreateFolder(maskFolderPath, avatarFolderName);
            }

            return avatarFolderPath;
        }

        private static string SanitizePathName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Unnamed";

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray()).Trim();

            return string.IsNullOrEmpty(sanitized) ? "Unnamed" : sanitized;
        }

        private static string BuildMaskFileName(GameObject avatarRoot, Renderer renderer, int materialSlot)
        {
            if (renderer == null)
                return $"Renderer_mat{materialSlot}_Mask.asset";

            var segments = new Stack<string>();
            var current = renderer.transform;
            var rootTransform = avatarRoot != null ? avatarRoot.transform : null;

            while (current != null && current != rootTransform)
            {
                segments.Push(SanitizePathName(current.name));
                current = current.parent;
            }

            if (segments.Count == 0)
            {
                segments.Push(SanitizePathName(renderer.gameObject.name));
            }

            string relativeName = string.Join("_", segments);
            return $"{relativeName}_mat{materialSlot}_Mask.asset";
        }

        private static Texture2D GenerateAndPersistMaskTexture(GameObject avatarRoot, Renderer renderer, int materialSlot, int uvChannel, Texture2D targetTexture)
        {
            if (avatarRoot == null || renderer == null || targetTexture == null)
                return null;

            var uvUsage = MeshUVAnalyzer.AnalyzeGameObjectUVUsage(renderer.gameObject, targetTexture, materialSlot, uvChannel);
            if (uvUsage == null || uvUsage.usedPixels == null)
                return null;

            var maskTexture = MeshUVAnalyzer.CreateBinaryMaskTexture(uvUsage);
            if (maskTexture == null)
                return null;

            var fileName = BuildMaskFileName(avatarRoot, renderer, materialSlot);
            maskTexture.name = Path.GetFileNameWithoutExtension(fileName);

            string folderPath = EnsureMaskFolder(avatarRoot);
            string assetPath = Path.Combine(folderPath, fileName).Replace("\\", "/");

            var existingAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (existingAsset != null)
            {
                EditorUtility.CopySerialized(maskTexture, existingAsset);
                EditorUtility.SetDirty(existingAsset);
                TextureColorSpaceUtility.UnregisterRuntimeTexture(maskTexture);
                UnityEngine.Object.DestroyImmediate(maskTexture);
                AssetDatabase.SaveAssets();
                return existingAsset;
            }

            AssetDatabase.CreateAsset(maskTexture, assetPath);
            EditorUtility.SetDirty(maskTexture);
            AssetDatabase.SaveAssets();
            return maskTexture;
        }

        private static GameObject EnsureMaterialHost(Transform containerTransform, Material targetMaterial)
        {
            if (containerTransform == null)
                return null;

            string materialName = targetMaterial != null ? SanitizePathName(targetMaterial.name) : "Material";
            string hostName = $"TexColAdj_{materialName}";

            var existing = containerTransform.Find(hostName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var host = new GameObject(hostName);
            Undo.RegisterCreatedObjectUndo(host, "Create NDMF material host");
            host.transform.SetParent(containerTransform, false);
            return host;
        }
    }
}
