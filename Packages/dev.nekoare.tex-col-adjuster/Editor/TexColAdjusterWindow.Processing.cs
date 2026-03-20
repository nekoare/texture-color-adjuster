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

            // Apply gamma correction (midpoint tone adjustment)
            if (gammaCorrection > 0f && gammaCorrection != 1f)
            {
                adjustedColor.r = Mathf.Clamp01(Mathf.Pow(adjustedColor.r, gammaCorrection));
                adjustedColor.g = Mathf.Clamp01(Mathf.Pow(adjustedColor.g, gammaCorrection));
                adjustedColor.b = Mathf.Clamp01(Mathf.Pow(adjustedColor.b, gammaCorrection));
            }

            // Apply midtone shift (shift histogram center)
            if (midtoneShift != 0f)
            {
                // Shift midtones using a curve: bias the luminance distribution
                // midtoneShift > 0 pushes midtones brighter, < 0 pushes darker
                // Uses a power curve centered on midtones (shadows/highlights less affected)
                float shift = midtoneShift;
                adjustedColor.r = ApplyMidtoneShift(adjustedColor.r, shift);
                adjustedColor.g = ApplyMidtoneShift(adjustedColor.g, shift);
                adjustedColor.b = ApplyMidtoneShift(adjustedColor.b, shift);
            }

            adjustedColor.a = color.a; // Preserve alpha
            return adjustedColor;
        }

        /// <summary>
        /// ミッドトーンシフト: 中間トーンを重点的にシフトし、シャドウ/ハイライトへの影響を抑える。
        /// ベルカーブ的な重みで中間値ほど大きくシフトされる。
        /// </summary>
        private static float ApplyMidtoneShift(float value, float shift)
        {
            // Weight: bell curve peaking at 0.5 (midtones affected most)
            float weight = 4f * value * (1f - value); // 0 at extremes, 1 at midpoint
            float shifted = value + shift * weight;
            return Mathf.Clamp01(shifted);
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
                
                // Step 1: LAB histogram matching (全体の色合わせ)
                Texture2D fullResolutionResult = ColorAdjuster.AdjustColors(
                    readableTarget,
                    readableReference,
                    adjustmentIntensity / 100f,
                    preserveLuminance,
                    adjustmentMode
                );

                // Step 2: DualSelection refinement (選択色域の追加補正)
                if (useDualColorSelection && hasSelectedTargetColor && hasSelectedReferenceColor && fullResolutionResult != null)
                {
                    var refined = ColorAdjuster.ApplyDualSelectionRefinement(
                        fullResolutionResult, selectedTargetColor, selectedReferenceColor, colorSelectionRange);
                    if (refined != null)
                    {
                        TextureColorSpaceUtility.UnregisterRuntimeTexture(fullResolutionResult);
                        UnityEngine.Object.DestroyImmediate(fullResolutionResult, true);
                        fullResolutionResult = refined;
                    }
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

                // Use uncompressed versions to avoid block noise
                var uncompressedRef = GetUncompressedTexture(referenceTexture, ref _cachedUncompressedRef, ref _cachedUncompressedRefSource, ref _refImportBackup);
                var uncompressedTarget = GetUncompressedTexture(targetTexture, ref _cachedUncompressedTarget, ref _cachedUncompressedTargetSource, ref _targetImportBackup);

                // Apply color adjustment process
                ApplyColorAdjustmentToMaterial(uncompressedRef ?? referenceTexture, uncompressedTarget ?? targetTexture, selectedTargetMaterial,
                    transferSourceMaterial, transferTargetMaterial);
                
                // The material transfer is now handled inside ApplyColorAdjustmentToMaterial
            }
            catch (Exception e)
            {
                Debug.LogError($"Experimental color adjustment failed: {e.Message}");
                EditorUtility.DisplayDialog("エラー", $"処理中にエラーが発生しました：{e.Message}", "OK");
            }
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
                Texture2D adjustedTexture = null;

                // Try GPU path first (same pipeline as preview)
                bool canUseGPU = GPUColorAdjuster.IsGPUProcessingAvailable()
                    && adjustmentMode == TexColAdjuster.Runtime.ColorAdjustmentMode.LabHistogramMatching;

                if (canUseGPU)
                {
                    var gpuResult = GPUColorAdjuster.AdjustColorsGPU(
                        targetTexture, referenceTexture,
                        adjustmentIntensity / 100f, preserveLuminance, adjustmentMode);

                    if (gpuResult != null)
                    {
                        // Apply HSBG on GPU if needed (same as preview)
                        RenderTexture finalRT = gpuResult;
                        if (HasPostAdjustmentsForWindow() && GPUColorAdjuster.IsHSBGGPUAvailable())
                        {
                            var hsbgResult = GPUColorAdjuster.ApplyHSBGOnGPU(
                                gpuResult, hueShift, saturationMultiplier, 1f, gammaCorrection, brightnessOffset, contrastMultiplier, midtoneShift);
                            if (hsbgResult != null)
                            {
                                gpuResult.Dispose();
                                finalRT = hsbgResult;
                            }
                        }

                        // ReadPixels to sRGB Texture2D for saving
                        adjustedTexture = ReadbackRenderTextureAsSRGB(finalRT, targetTexture);

                        // Apply DualSelection refinement on CPU
                        if (useDualColorSelection && hasSelectedTargetColor && hasSelectedReferenceColor && adjustedTexture != null)
                        {
                            var refined = ColorAdjuster.ApplyDualSelectionRefinement(
                                adjustedTexture, selectedTargetColor, selectedReferenceColor, colorSelectionRange);
                            if (refined != null)
                            {
                                TextureColorSpaceUtility.UnregisterRuntimeTexture(adjustedTexture);
                                UnityEngine.Object.DestroyImmediate(adjustedTexture, true);
                                adjustedTexture = refined;
                            }
                        }

                        if (finalRT is IDisposable d) d.Dispose();
                        else { finalRT.Release(); UnityEngine.Object.DestroyImmediate(finalRT); }

                        Debug.Log("[ApplyColorAdjustmentToMaterial] Used GPU pipeline");
                    }
                }

                // CPU fallback
                if (adjustedTexture == null)
                {
                    var readableTarget = TextureProcessor.MakeReadableCopy(targetTexture);
                    var readableReference = TextureProcessor.MakeReadableCopy(referenceTexture);

                    adjustedTexture = ColorAdjuster.AdjustColors(
                        readableTarget, readableReference,
                        adjustmentIntensity / 100f, preserveLuminance, adjustmentMode);

                    Debug.Log("[ApplyColorAdjustmentToMaterial] Used CPU fallback");
                }

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
                            // Restore original texture before recording Undo so the snapshot has the real original
                            RestoreScenePreview();

                            var undoTargets = new System.Collections.Generic.List<UnityEngine.Object> { targetMaterial };
                            if (enableMaterialTransfer && transferTargetMaterial != null && transferTargetMaterial != targetMaterial)
                                undoTargets.Add(transferTargetMaterial);
                            Undo.RecordObjects(undoTargets.ToArray(), "TexColAdjuster Apply");

                            if (!saveOptions.overwriteOriginal)
                            {
                                targetMaterial.SetTexture("_MainTex", savedTexture);
                                EditorUtility.SetDirty(targetMaterial);
                                AssetDatabase.SaveAssets();
                            }

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

                            RefreshAfterApply();
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
                            RestoreScenePreview();

                            var undoTargets = new System.Collections.Generic.List<UnityEngine.Object> { selectedTargetMaterial };
                            if (enableMaterialTransfer)
                            {
                                Material transferTargetMaterial = materialTransferDirection == 0 ? selectedTargetMaterial : selectedReferenceMaterial;
                                if (transferTargetMaterial != null && transferTargetMaterial != selectedTargetMaterial)
                                    undoTargets.Add(transferTargetMaterial);
                            }
                            Undo.RecordObjects(undoTargets.ToArray(), "TexColAdjuster Apply (High Precision)");

                            if (!saveOptions.overwriteOriginal)
                            {
                                selectedTargetMaterial.SetTexture("_MainTex", savedTexture);
                                EditorUtility.SetDirty(selectedTargetMaterial);
                                AssetDatabase.SaveAssets();
                            }

                            string hpSuccessMessage = "高精度色変換が正常に適用されました！";

                            if (enableMaterialTransfer && selectedReferenceMaterial != null && selectedTargetMaterial != null &&
                                IsLiltoonMaterial(selectedReferenceMaterial) && IsLiltoonMaterial(selectedTargetMaterial))
                            {
                                Material transferSourceMaterial = materialTransferDirection == 0 ? selectedReferenceMaterial : selectedTargetMaterial;
                                Material transferTargetMaterial = materialTransferDirection == 0 ? selectedTargetMaterial : selectedReferenceMaterial;

                                string directionText = materialTransferDirection == 0 ? "参照用 → 変更対象" : "変更対象 → 参照用";
                                LiltoonPresetApplier.TransferDrawingEffects(transferSourceMaterial, transferTargetMaterial, 1.0f);
                                hpSuccessMessage = $"高精度色変換とマテリアル設定転送が完了しました。\n転送方向: {directionText}";
                            }

                            RefreshAfterApply();
                            EditorUtility.DisplayDialog("成功", hpSuccessMessage, "OK");
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
                                RestoreScenePreview();
                                Undo.RecordObject(balanceSelectedMaterial, "TexColAdjuster Apply (Balance)");
                                balanceSelectedMaterial.SetTexture("_MainTex", savedTexture);
                                EditorUtility.SetDirty(balanceSelectedMaterial);
                                AssetDatabase.SaveAssets();
                            }
                        }

                        RefreshAfterApply();
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

        // Refresh window state after texture save/apply
        // Keeps the target (色を変えたい方) and resets everything else
        private void RefreshAfterApply()
        {
            DiscardScenePreview();
            ClearPreview();

            // Reset reference side (Direct tab)
            referenceGameObject = null;
            referenceComponent = null;
            selectedReferenceMaterial = null;
            referenceTexture = null;

            // Reset reference side (ColorAdjust tab)
            // balanceGameObject/balanceSelectedMaterial are the target side, keep them

            // Reset dual color selection
            useDualColorSelection = false;
            hasSelectedTargetColor = false;
            hasSelectedReferenceColor = false;
            selectedTargetColor = Color.white;
            selectedReferenceColor = Color.white;

            // Reset material transfer
            enableMaterialTransfer = false;

            // Force parameter cache to detect changes on next auto-preview
            lastAdjustmentIntensity = float.NaN;
            lastColorSelectionRange = -1f;
            lastUseHighPrecisionModeForPreview = !useHighPrecisionMode;

            Repaint();
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
    }
}
