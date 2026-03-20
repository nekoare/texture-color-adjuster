using System;
using TexColAdjuster.Editor.Models;
using TexColAdjuster.Runtime;
using UnityEngine;
using TexColAdjuster;

namespace TexColAdjuster.Editor
{
    /// <summary>
    /// GPU-based color adjustment using Compute Shaders
    /// Provides high-performance processing for real-time preview
    /// </summary>
    public static class GPUColorAdjuster
    {
        private static ComputeShader _labHistogramShader;
        private static ComputeShader _labStatisticsShader;
        private static ComputeShader _hsbgShader;
        private static bool _shadersLoaded = false;

        private const string HISTOGRAM_SHADER_PATH = "Packages/dev.nekoare.tex-col-adjuster/Editor/Shaders/LabHistogramMatching.compute";
        private const string STATISTICS_SHADER_PATH = "Packages/dev.nekoare.tex-col-adjuster/Editor/Shaders/LabStatistics.compute";

        private static void LoadShaders()
        {
            if (_shadersLoaded) return;

            _labHistogramShader = Resources.Load<ComputeShader>("LabHistogramMatching");
            _labStatisticsShader = Resources.Load<ComputeShader>("LabStatistics");
            _hsbgShader = Resources.Load<ComputeShader>("HSBGAdjustment");

            if (_labHistogramShader == null)
            {
                Debug.LogWarning("[TexColorAdjuster GPU] LabHistogramMatching.compute not found in Resources. Trying direct path...");
                _labHistogramShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(HISTOGRAM_SHADER_PATH);
            }

            if (_labStatisticsShader == null)
            {
                Debug.LogWarning("[TexColorAdjuster GPU] LabStatistics.compute not found in Resources. Trying direct path...");
                _labStatisticsShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(STATISTICS_SHADER_PATH);
            }

            _shadersLoaded = true;

            if (_labHistogramShader == null || _labStatisticsShader == null)
            {
                Debug.LogError("[TexColorAdjuster GPU] Failed to load compute shaders. GPU processing will not be available.");
            }
        }

        public static bool IsGPUProcessingAvailable()
        {
            LoadShaders();
            return _labHistogramShader != null && _labStatisticsShader != null && SystemInfo.supportsComputeShaders;
        }

        /// <summary>
        /// GPU版のカラー調整処理
        /// </summary>
        public static ExtendedRenderTexture AdjustColorsGPU(
            Texture2D targetTexture,
            Texture2D referenceTexture,
            float intensity,
            bool preserveLuminance,
            ColorAdjustmentMode mode,
            Texture2D targetUVMask = null,
            Texture2D referenceUVMask = null)
        {
            if (!IsGPUProcessingAvailable())
            {
                Debug.LogWarning("[TexColorAdjuster GPU] GPU processing is not available. Falling back to CPU.");
                return null;
            }

            if (targetTexture == null || referenceTexture == null)
            {
                Debug.LogError("[TexColorAdjuster GPU] Target or reference texture is null.");
                return null;
            }

            try
            {
                switch (mode)
                {
                    case ColorAdjustmentMode.LabHistogramMatching:
                        return LabHistogramMatchingGPU(targetTexture, referenceTexture, intensity, preserveLuminance, targetUVMask, referenceUVMask);

                    default:
                        Debug.LogWarning($"[TexColorAdjuster GPU] Mode {mode} is not yet implemented for GPU. Falling back to CPU.");
                        return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TexColorAdjuster GPU] GPU processing failed: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        private static ExtendedRenderTexture LabHistogramMatchingGPU(
            Texture2D targetTexture,
            Texture2D referenceTexture,
            float intensity,
            bool preserveLuminance,
            Texture2D targetUVMask = null,
            Texture2D referenceUVMask = null)
        {
            // All intermediate RTs are Linear: Blit handles sRGB→Linear decode,
            // compute shader works in linear space, no double sRGB decode/encode
            ExtendedRenderTexture targetRT = new ExtendedRenderTexture(targetTexture, RenderTextureReadWrite.Linear).Create(targetTexture);
            ExtendedRenderTexture referenceRT = new ExtendedRenderTexture(referenceTexture, RenderTextureReadWrite.Linear).Create(referenceTexture);
            ExtendedRenderTexture resultRT = new ExtendedRenderTexture(targetTexture, RenderTextureReadWrite.Linear).Create();

            try
            {
                // Step 1: Calculate statistics for target texture (UV masked if available)
                var targetStats = CalculateLabStatisticsGPU(targetRT, targetUVMask);

                // Step 2: Calculate statistics for reference texture (UV masked if available)
                var referenceStats = CalculateLabStatisticsGPU(referenceRT, referenceUVMask);

                // Step 3: Apply histogram matching
                int kernel = _labHistogramShader.FindKernel("LabHistogramMatching");

                _labHistogramShader.SetTexture(kernel, "TargetTexture", targetRT);
                _labHistogramShader.SetTexture(kernel, "ReferenceTexture", referenceRT);
                _labHistogramShader.SetTexture(kernel, "Result", resultRT);

                _labHistogramShader.SetVector("TargetStats", new Vector4(targetStats.lMean, targetStats.aMean, targetStats.bMean, 0));
                _labHistogramShader.SetVector("TargetStdDev", new Vector4(targetStats.lStd, targetStats.aStd, targetStats.bStd, 0));
                _labHistogramShader.SetVector("ReferenceStats", new Vector4(referenceStats.lMean, referenceStats.aMean, referenceStats.bMean, 0));
                _labHistogramShader.SetVector("ReferenceStdDev", new Vector4(referenceStats.lStd, referenceStats.aStd, referenceStats.bStd, 0));

                _labHistogramShader.SetFloat("Intensity", intensity);
                _labHistogramShader.SetFloat("PreserveLuminance", preserveLuminance ? 1.0f : 0.0f);
                _labHistogramShader.SetFloat("AlphaThreshold", ColorAdjuster.ALPHA_THRESHOLD);

                // Dispatch compute shader
                int threadGroupsX = Mathf.CeilToInt(targetTexture.width / 8.0f);
                int threadGroupsY = Mathf.CeilToInt(targetTexture.height / 8.0f);
                _labHistogramShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);

                // Clean up intermediate textures
                targetRT.Dispose();
                referenceRT.Dispose();

                return resultRT;
            }
            catch (Exception ex)
            {
                targetRT?.Dispose();
                referenceRT?.Dispose();
                resultRT?.Dispose();
                throw new Exception($"GPU histogram matching failed: {ex.Message}", ex);
            }
        }

        private struct LabStatistics
        {
            public float lMean, aMean, bMean;
            public float lStd, aStd, bStd;
        }

        private static LabStatistics CalculateLabStatisticsGPU(RenderTexture texture, Texture uvMask = null)
        {
            int width = texture.width;
            int height = texture.height;

            // Create buffers for reduction (4 floats: L, a, b, count/unused)
            ComputeBuffer meanBuffer = new ComputeBuffer(4, sizeof(float));
            ComputeBuffer stdDevBuffer = new ComputeBuffer(4, sizeof(float));

            // Create a dummy 1x1 black texture if no UV mask provided
            Texture2D dummyMask = null;
            Texture maskTex = uvMask;
            if (maskTex == null)
            {
                dummyMask = new Texture2D(1, 1, TextureFormat.R8, false, true);
                dummyMask.SetPixel(0, 0, Color.black);
                dummyMask.Apply();
                maskTex = dummyMask;
            }

            try
            {
                // Initialize buffers
                meanBuffer.SetData(new float[] { 0f, 0f, 0f, 0f });
                stdDevBuffer.SetData(new float[] { 0f, 0f, 0f, 0f });

                // Step 1: Calculate mean
                int meanKernel = _labStatisticsShader.FindKernel("ComputeMean");
                _labStatisticsShader.SetTexture(meanKernel, "InputTexture", texture);
                _labStatisticsShader.SetTexture(meanKernel, "UVMask", maskTex);
                _labStatisticsShader.SetBuffer(meanKernel, "MeanBuffer", meanBuffer);
                _labStatisticsShader.SetFloat("AlphaThreshold", ColorAdjuster.ALPHA_THRESHOLD);
                _labStatisticsShader.SetInt("UseUVMask", uvMask != null ? 1 : 0);

                int threadGroupsX = Mathf.CeilToInt(width / 8.0f);
                int threadGroupsY = Mathf.CeilToInt(height / 8.0f);
                _labStatisticsShader.Dispatch(meanKernel, threadGroupsX, threadGroupsY, 1);

                // Read mean values
                float[] meanResult = new float[4];
                meanBuffer.GetData(meanResult);

                float opaquePixelCount = Mathf.Max(meanResult[3], 1.0f); // Avoid division by zero
                LabStatistics stats = new LabStatistics
                {
                    lMean = meanResult[0] / opaquePixelCount,
                    aMean = meanResult[1] / opaquePixelCount,
                    bMean = meanResult[2] / opaquePixelCount
                };

                // Step 2: Calculate standard deviation
                int stdDevKernel = _labStatisticsShader.FindKernel("ComputeStdDev");
                _labStatisticsShader.SetTexture(stdDevKernel, "InputTexture", texture);
                _labStatisticsShader.SetTexture(stdDevKernel, "UVMask", maskTex);
                _labStatisticsShader.SetBuffer(stdDevKernel, "StdDevBuffer", stdDevBuffer);
                _labStatisticsShader.SetVector("MeanValues", new Vector4(stats.lMean, stats.aMean, stats.bMean, 0));
                _labStatisticsShader.SetFloat("AlphaThreshold", ColorAdjuster.ALPHA_THRESHOLD);
                _labStatisticsShader.SetInt("UseUVMask", uvMask != null ? 1 : 0);

                _labStatisticsShader.Dispatch(stdDevKernel, threadGroupsX, threadGroupsY, 1);

                // Read standard deviation values
                float[] stdDevResult = new float[4];
                stdDevBuffer.GetData(stdDevResult);

                stats.lStd = Mathf.Sqrt(stdDevResult[0] / opaquePixelCount);
                stats.aStd = Mathf.Sqrt(stdDevResult[1] / opaquePixelCount);
                stats.bStd = Mathf.Sqrt(stdDevResult[2] / opaquePixelCount);

                return stats;
            }
            finally
            {
                meanBuffer?.Release();
                stdDevBuffer?.Release();
                if (dummyMask != null)
                    UnityEngine.Object.DestroyImmediate(dummyMask);
            }
        }

        /// <summary>
        /// Apply HSBG post-adjustments on GPU. Input RenderTexture is consumed and a new result is returned.
        /// </summary>
        public static ExtendedRenderTexture ApplyHSBGOnGPU(
            RenderTexture inputRT,
            float hueShift,
            float saturation,
            float brightness,
            float gamma,
            float brightnessOffset = 0f,
            float contrast = 1f,
            float midtoneShift = 0f)
        {
            LoadShaders();

            if (_hsbgShader == null || !SystemInfo.supportsComputeShaders)
                return null;

            try
            {
                var resultRT = new ExtendedRenderTexture(inputRT.width, inputRT.height, RenderTextureReadWrite.Linear).Create();

                int kernel = _hsbgShader.FindKernel("HSBGAdjust");

                _hsbgShader.SetTexture(kernel, "InputTexture", inputRT);
                _hsbgShader.SetTexture(kernel, "Result", resultRT);
                _hsbgShader.SetFloat("HueShift", hueShift);
                _hsbgShader.SetFloat("Saturation", saturation);
                _hsbgShader.SetFloat("Brightness", brightness);
                _hsbgShader.SetFloat("Gamma", gamma);
                _hsbgShader.SetFloat("BrightnessOffset", brightnessOffset);
                _hsbgShader.SetFloat("Contrast", contrast);
                _hsbgShader.SetFloat("MidtoneShift", midtoneShift);

                int threadGroupsX = Mathf.CeilToInt(inputRT.width / 8.0f);
                int threadGroupsY = Mathf.CeilToInt(inputRT.height / 8.0f);
                _hsbgShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);

                return resultRT;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TexColorAdjuster GPU] HSBG processing failed: {ex.Message}");
                return null;
            }
        }

        public static bool IsHSBGGPUAvailable()
        {
            LoadShaders();
            return _hsbgShader != null && SystemInfo.supportsComputeShaders;
        }
    }
}
