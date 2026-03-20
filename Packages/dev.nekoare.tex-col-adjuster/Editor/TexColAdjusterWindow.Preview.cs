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

        private Texture2D GenerateUVMaskForComponent(Component component, Material material, Texture2D texture)
        {
            if (component == null || texture == null) return null;
            if (!(component is Renderer renderer)) return null;

            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer smr)
                mesh = smr.sharedMesh;
            else if (renderer is MeshRenderer mr)
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }
            if (mesh == null) return null;

            // Find material slot that uses this material
            var mats = renderer.sharedMaterials;
            int matIndex = -1;

            // Direct match (material reference)
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == material) { matIndex = i; break; }
            }

            // Fallback: match by _MainTex, but only if unique
            if (matIndex < 0)
            {
                int matchCount = 0;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].HasProperty("_MainTex") && mats[i].mainTexture == texture)
                    {
                        if (matIndex < 0) matIndex = i;
                        matchCount++;
                    }
                }
                // Multiple materials share this texture - can't determine submesh
                if (matchCount > 1) matIndex = -1;
            }

            if (matIndex < 0) return null;

            return UVMapGenerator.GenerateUVMask(mesh, texture.width, texture.height, matIndex);
        }

        private void GeneratePreview()
        {
            if (!CanProcess()) return;

            isProcessing = true;
            processingProgress = 0f;
            bool previewGenerated = false;

            // Restore material before disposing GPU texture to avoid dangling reference
            RestoreScenePreview();
            DisposeScenePreviewRenderTexture();

            try
            {
                UpdateOriginalTexture();

                processingProgress = 0.2f;
                Repaint();

                bool usedGPU = false;

                // Generate UV masks for statistics filtering (Direct tab only)
                if (_cachedTargetUVMask != null) { UnityEngine.Object.DestroyImmediate(_cachedTargetUVMask, true); _cachedTargetUVMask = null; }
                if (_cachedReferenceUVMask != null) { UnityEngine.Object.DestroyImmediate(_cachedReferenceUVMask, true); _cachedReferenceUVMask = null; }
                if (activeTab == 1) // Direct tab
                {
                    _cachedTargetUVMask = GenerateUVMaskForComponent(targetComponent, selectedTargetMaterial, targetTexture);
                    _cachedReferenceUVMask = GenerateUVMaskForComponent(referenceComponent, selectedReferenceMaterial, referenceTexture);
                }

                // Try GPU path first (LabHistogramMatching)
                bool canUseGPU = GPUColorAdjuster.IsGPUProcessingAvailable()
                    && adjustmentMode == TexColAdjuster.Runtime.ColorAdjustmentMode.LabHistogramMatching;

                if (canUseGPU)
                {
                    var gpuResult = GPUColorAdjuster.AdjustColorsGPU(
                        targetTexture,
                        referenceTexture,
                        adjustmentIntensity / 100f,
                        preserveLuminance,
                        adjustmentMode,
                        _cachedTargetUVMask,
                        _cachedReferenceUVMask
                    );

                    if (gpuResult != null)
                    {
                        // Cache LAB matching result for fast post-adjustment updates
                        DisposeCachedLabMatchRT();
                        _cachedLabMatchRT = gpuResult;

                        // Apply HSBG on GPU if needed
                        Texture finalResult = gpuResult;
                        if (HasPostAdjustmentsForWindow() && GPUColorAdjuster.IsHSBGGPUAvailable())
                        {
                            var hsbgResult = GPUColorAdjuster.ApplyHSBGOnGPU(
                                gpuResult, hueShift, saturationMultiplier, 1f, gammaCorrection, brightnessOffset, contrastMultiplier, midtoneShift);
                            if (hsbgResult != null)
                            {
                                finalResult = hsbgResult;
                            }
                        }

                        // For Scene preview: keep RenderTexture alive
                        scenePreviewRenderTexture = finalResult;
                        usedGPU = true;

                        // Readback for window preview or DualSelection post-processing
                        bool needReadback = showWindowPreview || activeTab == 0
                            || (useDualColorSelection && hasSelectedTargetColor && hasSelectedReferenceColor);
                        if (needReadback)
                        {
                            previewTexture = ReadbackRenderTexture(finalResult as RenderTexture);
                        }

                        // Apply DualSelection refinement on CPU (LAB matched result + local color correction)
                        if (useDualColorSelection && hasSelectedTargetColor && hasSelectedReferenceColor && previewTexture != null)
                        {
                            var refined = ColorAdjuster.ApplyDualSelectionRefinement(
                                previewTexture, selectedTargetColor, selectedReferenceColor, colorSelectionRange);
                            if (refined != null)
                            {
                                TextureColorSpaceUtility.UnregisterRuntimeTexture(previewTexture);
                                UnityEngine.Object.DestroyImmediate(previewTexture, true);
                                previewTexture = refined;

                                // Update Scene preview with DualSelection result
                                // Blit CPU result to a new RT for scene preview
                                var dualRT = RenderTexture.GetTemporary(
                                    refined.width, refined.height, 0,
                                    RenderTextureFormat.ARGB32,
                                    RenderTextureReadWrite.sRGB);
                                Graphics.Blit(refined, dualRT);

                                // Dispose previous scene preview RT (but keep LAB cache)
                                if (scenePreviewRenderTexture != null && scenePreviewRenderTexture != _cachedLabMatchRT)
                                {
                                    if (scenePreviewRenderTexture is System.IDisposable disp) disp.Dispose();
                                    else if (scenePreviewRenderTexture is RenderTexture oldRT)
                                    {
                                        oldRT.Release();
                                        UnityEngine.Object.DestroyImmediate(oldRT);
                                    }
                                }
                                scenePreviewRenderTexture = dualRT;
                            }
                        }

                        previewGenerated = true;
                    }
                }

                // CPU fallback
                if (!usedGPU)
                {
                    processingProgress = 0.4f;
                    Repaint();

                    var readableTarget = TextureProcessor.MakeReadableCopy(targetTexture);
                    var readableReference = TextureProcessor.MakeReadableCopy(referenceTexture);

                    if (readableTarget == null || readableReference == null)
                    {
                        if (readableTarget != null) UnityEngine.Object.DestroyImmediate(readableTarget, true);
                        if (readableReference != null) UnityEngine.Object.DestroyImmediate(readableReference, true);
                        throw new Exception("Could not create readable texture copies.");
                    }

                    // Step 1: LAB histogram matching (全体の色合わせ)
                    previewTexture = ColorAdjuster.AdjustColors(
                        readableTarget, readableReference,
                        adjustmentIntensity / 100f, preserveLuminance,
                        adjustmentMode
                    );

                    // Step 2: DualSelection refinement (選択色域の追加補正)
                    if (useDualColorSelection && hasSelectedTargetColor && hasSelectedReferenceColor && previewTexture != null)
                    {
                        var refined = ColorAdjuster.ApplyDualSelectionRefinement(
                            previewTexture, selectedTargetColor, selectedReferenceColor, colorSelectionRange);
                        if (refined != null)
                        {
                            TextureColorSpaceUtility.UnregisterRuntimeTexture(previewTexture);
                            UnityEngine.Object.DestroyImmediate(previewTexture, true);
                            previewTexture = refined;
                        }
                    }

                    UnityEngine.Object.DestroyImmediate(readableTarget, true);
                    UnityEngine.Object.DestroyImmediate(readableReference, true);

                    // Cache CPU base texture for fast post-adjustment re-application
                    if (_cachedCpuBaseTexture != null)
                    {
                        UnityEngine.Object.DestroyImmediate(_cachedCpuBaseTexture, true);
                    }
                    if (previewTexture != null)
                    {
                        _cachedCpuBaseTexture = UnityEngine.Object.Instantiate(previewTexture);
                    }

                    // Step 3: Apply post-adjustments (brightness, saturation, gamma, midtone shift)
                    if (previewTexture != null && HasPostAdjustmentsForWindow())
                    {
                        Color[] pixels = previewTexture.GetPixels();
                        for (int i = 0; i < pixels.Length; i++)
                        {
                            Color c = pixels[i];
                            float a = c.a;

                            // Gamma
                            if (Mathf.Abs(gammaCorrection - 1f) > 0.001f)
                            {
                                c.r = Mathf.Clamp01(Mathf.Pow(c.r, gammaCorrection));
                                c.g = Mathf.Clamp01(Mathf.Pow(c.g, gammaCorrection));
                                c.b = Mathf.Clamp01(Mathf.Pow(c.b, gammaCorrection));
                            }

                            // Saturation
                            if (Mathf.Abs(saturationMultiplier - 1f) > 0.001f)
                            {
                                float gray = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                                c.r = Mathf.Clamp01(gray + (c.r - gray) * saturationMultiplier);
                                c.g = Mathf.Clamp01(gray + (c.g - gray) * saturationMultiplier);
                                c.b = Mathf.Clamp01(gray + (c.b - gray) * saturationMultiplier);
                            }

                            // Brightness offset
                            if (Mathf.Abs(brightnessOffset) > 0.001f)
                            {
                                c.r = Mathf.Clamp01(c.r + brightnessOffset);
                                c.g = Mathf.Clamp01(c.g + brightnessOffset);
                                c.b = Mathf.Clamp01(c.b + brightnessOffset);
                            }

                            // Midtone shift
                            if (Mathf.Abs(midtoneShift) > 0.001f)
                            {
                                float wr = 4f * c.r * (1f - c.r);
                                float wg = 4f * c.g * (1f - c.g);
                                float wb = 4f * c.b * (1f - c.b);
                                c.r = Mathf.Clamp01(c.r + midtoneShift * wr);
                                c.g = Mathf.Clamp01(c.g + midtoneShift * wg);
                                c.b = Mathf.Clamp01(c.b + midtoneShift * wb);
                            }

                            c.a = a;
                            pixels[i] = c;
                        }
                        previewTexture.SetPixels(pixels);
                        previewTexture.Apply();
                    }

                    previewGenerated = previewTexture != null;
                }

                processingProgress = 1f;
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

                    // Auto-apply to Scene view (GPU RenderTexture or CPU Texture2D)
                    var sceneTexture = scenePreviewRenderTexture != null ? scenePreviewRenderTexture : (Texture)previewTexture;
                    if (sceneTexture != null)
                    {
                        ApplyScenePreview(sceneTexture);
                    }
                }
            }
        }


        private bool HasPostAdjustmentsForWindow()
        {
            const float epsilon = 0.001f;
            return Mathf.Abs(hueShift) > epsilon
                || Mathf.Abs(saturationMultiplier - 1f) > epsilon
                || Mathf.Abs(brightnessOffset) > epsilon
                || Mathf.Abs(contrastMultiplier - 1f) > epsilon
                || Mathf.Abs(gammaCorrection - 1f) > epsilon
                || Mathf.Abs(midtoneShift) > epsilon;
        }


        /// <summary>
        /// ポスト調整パラメータ（Gamma, MidtoneShift等）だけが変わったかを判定。
        /// LABマッチングに関わるパラメータは変わっていない場合trueを返す。
        /// </summary>
        private bool HasOnlyPostAdjustmentChanged()
        {
            bool coreUnchanged =
                Mathf.Approximately(lastAdjustmentIntensity, adjustmentIntensity) &&
                lastPreserveLuminance == preserveLuminance &&
                lastPreserveTexture == preserveTexture &&
                lastAdjustmentMode == adjustmentMode &&
                lastUseDualColorSelection == useDualColorSelection &&
                lastSelectedTargetColor == selectedTargetColor &&
                lastSelectedReferenceColor == selectedReferenceColor &&
                lastHasSelectedTargetColor == hasSelectedTargetColor &&
                lastHasSelectedReferenceColor == hasSelectedReferenceColor &&
                Mathf.Approximately(lastColorSelectionRange, colorSelectionRange) &&
                lastUseHighPrecisionModeForPreview == useHighPrecisionMode;

            bool postChanged =
                !Mathf.Approximately(lastGammaCorrection, gammaCorrection) ||
                !Mathf.Approximately(lastMidtoneShift, midtoneShift) ||
                !Mathf.Approximately(lastHueShift, hueShift) ||
                !Mathf.Approximately(lastSaturationMultiplier, saturationMultiplier) ||
                !Mathf.Approximately(lastBrightnessOffset, brightnessOffset) ||
                !Mathf.Approximately(lastContrastMultiplier, contrastMultiplier);

            return coreUnchanged && postChanged;
        }


        /// <summary>
        /// キャッシュ済みLABマッチング結果にHSBGだけ再適用する（即時実行）。
        /// GPU RTキャッシュまたはCPU Texture2Dキャッシュを使用。
        /// </summary>
        private void ReapplyPostAdjustmentsFromCache()
        {
            // GPU path: cached RT available
            if (_cachedLabMatchRT != null)
            {
                // Dispose previous scene preview (but not the cached LAB result)
                if (scenePreviewRenderTexture != null && scenePreviewRenderTexture != _cachedLabMatchRT)
                {
                    if (scenePreviewRenderTexture is IDisposable d) d.Dispose();
                    else { ((RenderTexture)scenePreviewRenderTexture).Release(); UnityEngine.Object.DestroyImmediate(scenePreviewRenderTexture); }
                }

                Texture finalResult = _cachedLabMatchRT;

                if (HasPostAdjustmentsForWindow() && GPUColorAdjuster.IsHSBGGPUAvailable())
                {
                    var hsbgResult = GPUColorAdjuster.ApplyHSBGOnGPU(
                        _cachedLabMatchRT, hueShift, saturationMultiplier, 1f, gammaCorrection, brightnessOffset, contrastMultiplier, midtoneShift);
                    if (hsbgResult != null)
                    {
                        finalResult = hsbgResult;
                    }
                }

                scenePreviewRenderTexture = finalResult;

                if (showWindowPreview || activeTab == 0)
                {
                    if (previewTexture != null)
                    {
                        TextureColorSpaceUtility.UnregisterRuntimeTexture(previewTexture);
                        UnityEngine.Object.DestroyImmediate(previewTexture, true);
                    }
                    previewTexture = ReadbackRenderTexture(finalResult as RenderTexture);
                }

                RefreshScenePreviewMaterial();
                UpdateParameterCache();
                return;
            }

            // CPU path: cached base texture available
            if (_cachedCpuBaseTexture != null)
            {
                if (previewTexture != null)
                {
                    TextureColorSpaceUtility.UnregisterRuntimeTexture(previewTexture);
                    UnityEngine.Object.DestroyImmediate(previewTexture, true);
                }

                previewTexture = UnityEngine.Object.Instantiate(_cachedCpuBaseTexture);

                if (HasPostAdjustmentsForWindow())
                {
                    Color[] pixels = previewTexture.GetPixels();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        Color c = pixels[i];
                        float a = c.a;

                        if (Mathf.Abs(gammaCorrection - 1f) > 0.001f)
                        {
                            c.r = Mathf.Clamp01(Mathf.Pow(c.r, gammaCorrection));
                            c.g = Mathf.Clamp01(Mathf.Pow(c.g, gammaCorrection));
                            c.b = Mathf.Clamp01(Mathf.Pow(c.b, gammaCorrection));
                        }
                        if (Mathf.Abs(saturationMultiplier - 1f) > 0.001f)
                        {
                            float gray = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                            c.r = Mathf.Clamp01(gray + (c.r - gray) * saturationMultiplier);
                            c.g = Mathf.Clamp01(gray + (c.g - gray) * saturationMultiplier);
                            c.b = Mathf.Clamp01(gray + (c.b - gray) * saturationMultiplier);
                        }
                        if (Mathf.Abs(brightnessOffset) > 0.001f)
                        {
                            c.r = Mathf.Clamp01(c.r + brightnessOffset);
                            c.g = Mathf.Clamp01(c.g + brightnessOffset);
                            c.b = Mathf.Clamp01(c.b + brightnessOffset);
                        }
                        if (Mathf.Abs(midtoneShift) > 0.001f)
                        {
                            float wr = 4f * c.r * (1f - c.r);
                            float wg = 4f * c.g * (1f - c.g);
                            float wb = 4f * c.b * (1f - c.b);
                            c.r = Mathf.Clamp01(c.r + midtoneShift * wr);
                            c.g = Mathf.Clamp01(c.g + midtoneShift * wg);
                            c.b = Mathf.Clamp01(c.b + midtoneShift * wb);
                        }

                        c.a = a;
                        pixels[i] = c;
                    }
                    previewTexture.SetPixels(pixels);
                    previewTexture.Apply();
                }

                // Update scene preview with CPU result
                var cpuRT = RenderTexture.GetTemporary(
                    previewTexture.width, previewTexture.height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(previewTexture, cpuRT);

                if (scenePreviewRenderTexture != null)
                {
                    if (scenePreviewRenderTexture is IDisposable d2) d2.Dispose();
                    else if (scenePreviewRenderTexture is RenderTexture oldRT)
                    {
                        oldRT.Release();
                        UnityEngine.Object.DestroyImmediate(oldRT);
                    }
                }
                scenePreviewRenderTexture = cpuRT;

                RefreshScenePreviewMaterial();
                UpdateParameterCache();
                Repaint();
                return;
            }
        }


        private void DisposeCachedLabMatchRT()
        {
            if (_cachedLabMatchRT != null)
            {
                // scenePreviewRenderTextureが同じ参照を持っていたら先にnullにする
                if (scenePreviewRenderTexture == _cachedLabMatchRT)
                    scenePreviewRenderTexture = null;
                _cachedLabMatchRT.Release();
                UnityEngine.Object.DestroyImmediate(_cachedLabMatchRT);
                _cachedLabMatchRT = null;
            }
            if (_cachedCpuBaseTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(_cachedCpuBaseTexture, true);
                _cachedCpuBaseTexture = null;
            }
        }

        private void DisposeUVMasks()
        {
            if (_cachedTargetUVMask != null)
            {
                UnityEngine.Object.DestroyImmediate(_cachedTargetUVMask, true);
                _cachedTargetUVMask = null;
            }
            if (_cachedReferenceUVMask != null)
            {
                UnityEngine.Object.DestroyImmediate(_cachedReferenceUVMask, true);
                _cachedReferenceUVMask = null;
            }
        }


        /// <summary>
        /// 圧縮テクスチャを一時的に非圧縮にしてキャッシュする。
        /// 同じテクスチャが既にキャッシュされていればそれを返す。
        /// </summary>
        private Texture2D GetUncompressedTexture(Texture2D source, ref Texture2D cachedTexture, ref Texture2D cachedSource, ref TextureImportBackup backup)
        {
            if (source == null) return null;

            // Cache hit
            if (cachedSource == source && cachedTexture != null)
                return cachedTexture;

            // Restore previous backup if exists
            if (backup != null)
            {
                backup.RestoreSettings();
                backup = null;
            }

            // Check if texture needs decompression
            bool isCompressed = source.format == TextureFormat.DXT1 ||
                                source.format == TextureFormat.DXT1Crunched ||
                                source.format == TextureFormat.DXT5 ||
                                source.format == TextureFormat.DXT5Crunched ||
                                source.format == TextureFormat.BC7 ||
                                source.format == TextureFormat.BC5 ||
                                source.format == TextureFormat.BC4 ||
                                source.format == TextureFormat.BC6H;

            if (!isCompressed)
            {
                cachedSource = source;
                cachedTexture = source;
                return source;
            }

            // Make texture readable and uncompressed via reimport
            var result = TextureProcessor.MakeTextureReadable(source, out backup);
            cachedSource = source;
            cachedTexture = result ?? source;
            return cachedTexture;
        }


        /// <summary>
        /// キャッシュ済み非圧縮テクスチャを破棄し、元のインポート設定を復元する。
        /// </summary>
        private void RestoreUncompressedTextureCache()
        {
            if (_targetImportBackup != null)
            {
                _targetImportBackup.RestoreSettings();
                _targetImportBackup = null;
            }
            if (_refImportBackup != null)
            {
                _refImportBackup.RestoreSettings();
                _refImportBackup = null;
            }
            _cachedUncompressedTarget = null;
            _cachedUncompressedRef = null;
            _cachedUncompressedTargetSource = null;
            _cachedUncompressedRefSource = null;
        }


        private Texture2D ReadbackRenderTexture(RenderTexture rt)
        {
            if (rt == null) return null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = TextureColorSpaceUtility.CreateRuntimeTextureLike(rt);
            tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }


        /// <summary>
        /// GPU結果(Linear RT)をsRGB Texture2Dとして読み戻す。
        /// Linear→sRGB変換をBlit経由で行い、PNG保存時に正しいsRGBデータになるようにする。
        /// </summary>
        private Texture2D ReadbackRenderTextureAsSRGB(RenderTexture rt, Texture2D originalTexture)
        {
            if (rt == null) return null;

            // Blit Linear RT → sRGB RT (GPU handles Linear→sRGB encode)
            var srgbRT = RenderTexture.GetTemporary(
                rt.width, rt.height, 0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            Graphics.Blit(rt, srgbRT);

            // ReadPixels from sRGB RT into sRGB Texture2D
            var prev = RenderTexture.active;
            RenderTexture.active = srgbRT;
            var tex = TextureColorSpaceUtility.CreateRuntimeTextureLike(originalTexture);
            tex.ReadPixels(new UnityEngine.Rect(0, 0, srgbRT.width, srgbRT.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(srgbRT);

            return tex;
        }


        private void DisposeScenePreviewRenderTexture()
        {
            if (scenePreviewRenderTexture != null)
            {
                // scenePreviewRenderTextureがキャッシュと別の場合のみ破棄
                if (scenePreviewRenderTexture != _cachedLabMatchRT)
                {
                    if (scenePreviewRenderTexture is IDisposable d)
                        d.Dispose();
                    else if (scenePreviewRenderTexture is RenderTexture rt)
                    {
                        rt.Release();
                        UnityEngine.Object.DestroyImmediate(rt);
                    }
                }
                scenePreviewRenderTexture = null;
            }
            DisposeCachedLabMatchRT();
        }

        
        
        private void GenerateExperimentalPreview()
        {
            if (!CanProcessExperimental()) return;

            // Get main color textures from the selected materials
            var refTexture = GetExperimentalReferenceTexture();
            var targetTexture = GetExperimentalTargetTexture();

            if (refTexture == null || targetTexture == null) return;

            // Get uncompressed versions to avoid block noise from compressed formats (DXT etc.)
            var uncompressedTarget = GetUncompressedTexture(targetTexture, ref _cachedUncompressedTarget, ref _cachedUncompressedTargetSource, ref _targetImportBackup);
            var uncompressedRef = GetUncompressedTexture(refTexture, ref _cachedUncompressedRef, ref _cachedUncompressedRefSource, ref _refImportBackup);

            // Use the existing preview generation logic with main color textures
            var originalRefTexture = referenceTexture;
            var originalTargetTexture = this.targetTexture;

            // Temporarily set the uncompressed textures for the preview generation
            referenceTexture = uncompressedRef ?? refTexture;
            this.targetTexture = uncompressedTarget ?? targetTexture;

            GeneratePreview();

            // Restore original textures
            referenceTexture = originalRefTexture;
            this.targetTexture = originalTargetTexture;
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
                Vector3 targetLab = ColorSpaceConverter.RGBtoLAB(selectedTargetColor.linear);
                Vector3 referenceLab = ColorSpaceConverter.RGBtoLAB(selectedReferenceColor.linear);
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
                
                // Get hover color (using cached readable texture to avoid per-frame allocation)
                try
                {
                    var readableTexture = GetReadableTextureForPicking(targetTexture, ref cachedTargetReadableForPicking, ref cachedTargetReadableSource);
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
            if (CanProcessSmartColorMatch())
            {
                EditorApplication.delayCall += () => {
                    if (CanProcessSmartColorMatch())
                    {
                        GenerateSmartColorMatchPreview();
                    }
                };
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

                if (previewTexture != null)
                {
                    // Auto-apply to Scene view
                    ApplyScenePreview(previewTexture);
                }
                else
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

        
        private void ApplyScenePreview(Texture preview, Material targetMaterial = null)
        {
            // Resolve target material and renderer
            var material = targetMaterial ?? selectedTargetMaterial ?? balanceSelectedMaterial;
            var renderer = ResolveRendererForMaterial(material);
            if (preview == null || material == null || renderer == null)
                return;

            // If switching to a different material/renderer, restore first
            if (scenePreviewActive && (scenePreviewOriginalMaterial != material || scenePreviewRenderer != renderer))
            {
                RestoreScenePreview();
            }

            // Save original state if not yet active
            if (!scenePreviewActive)
            {
                scenePreviewRenderer = renderer;
                scenePreviewOriginalMaterial = material;
                scenePreviewMaterialSlot = FindMaterialSlot(renderer, material);
                scenePreviewActive = true;
            }

            // Create or update cloned material
            if (scenePreviewClonedMaterial != null)
                UnityEngine.Object.DestroyImmediate(scenePreviewClonedMaterial);

            scenePreviewClonedMaterial = UnityEngine.Object.Instantiate(material);
            scenePreviewClonedMaterial.hideFlags = HideFlags.HideAndDontSave;
            scenePreviewClonedMaterial.SetTexture("_MainTex", preview);

            // Apply material settings transfer preview if enabled
            if (enableMaterialTransfer && selectedReferenceMaterial != null &&
                IsLiltoonMaterial(selectedReferenceMaterial) && IsLiltoonMaterial(material))
            {
                Material transferSource = materialTransferDirection == 0 ? selectedReferenceMaterial : material;
                LiltoonPresetApplier.TransferDrawingEffects(transferSource, scenePreviewClonedMaterial, 1.0f);
            }

            // Swap material on renderer
            var materials = renderer.sharedMaterials;
            if (scenePreviewMaterialSlot >= 0 && scenePreviewMaterialSlot < materials.Length)
            {
                materials[scenePreviewMaterialSlot] = scenePreviewClonedMaterial;
                renderer.sharedMaterials = materials;
            }

            SceneView.RepaintAll();
        }


        private void RestoreScenePreview()
        {
            if (!scenePreviewActive)
                return;

            if (scenePreviewRenderer != null && scenePreviewOriginalMaterial != null)
            {
                var materials = scenePreviewRenderer.sharedMaterials;
                if (scenePreviewMaterialSlot >= 0 && scenePreviewMaterialSlot < materials.Length)
                {
                    materials[scenePreviewMaterialSlot] = scenePreviewOriginalMaterial;
                    scenePreviewRenderer.sharedMaterials = materials;
                }
            }

            if (scenePreviewClonedMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(scenePreviewClonedMaterial);
                scenePreviewClonedMaterial = null;
            }

            scenePreviewRenderer = null;
            scenePreviewOriginalMaterial = null;
            scenePreviewMaterialSlot = -1;
            scenePreviewActive = false;
            SceneView.RepaintAll();
        }


        // Discard scene preview state without restoring the original material
        // Use after applying changes so the new state is kept
        private void DiscardScenePreview()
        {
            if (scenePreviewClonedMaterial != null)
            {
                // Restore original material first since we're discarding, not applying the clone
                if (scenePreviewRenderer != null && scenePreviewOriginalMaterial != null)
                {
                    var materials = scenePreviewRenderer.sharedMaterials;
                    if (scenePreviewMaterialSlot >= 0 && scenePreviewMaterialSlot < materials.Length)
                    {
                        materials[scenePreviewMaterialSlot] = scenePreviewOriginalMaterial;
                        scenePreviewRenderer.sharedMaterials = materials;
                    }
                }
                UnityEngine.Object.DestroyImmediate(scenePreviewClonedMaterial);
                scenePreviewClonedMaterial = null;
            }

            scenePreviewRenderer = null;
            scenePreviewOriginalMaterial = null;
            scenePreviewMaterialSlot = -1;
            scenePreviewActive = false;
            DisposeScenePreviewRenderTexture();
        }


        // Re-apply scene preview with current material transfer settings (no texture recompute)
        private void RefreshScenePreviewMaterial()
        {
            if (!scenePreviewActive)
                return;

            // Determine the preview texture currently in use
            Texture preview = scenePreviewRenderTexture ?? (Texture)previewTexture;
            if (preview == null)
                return;

            // Re-apply will rebuild the cloned material with updated transfer settings
            var material = scenePreviewOriginalMaterial;
            var renderer = scenePreviewRenderer;
            if (material == null || renderer == null)
                return;

            // Temporarily clear active state so ApplyScenePreview treats this as fresh
            RestoreScenePreview();
            ApplyScenePreview(preview, material);
        }


        private void ClearPreview()
        {
            RestoreScenePreview();
            DisposeScenePreviewRenderTexture();

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
            DisposeCachedLabMatchRT();
            DisposeUVMasks();
            lastAdjustmentIntensity = float.NaN;
            lastColorSelectionRange = -1f;
            lastUseHighPrecisionModeForPreview = !useHighPrecisionMode;
            directTabHasQueuedParameters = false;
            directTabPreviewPending = false;
            directTabPreviewInFlight = false;
            directTabQueuedParameterState = default;
            
            ClearUVMaskPreview();
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

    }
}
