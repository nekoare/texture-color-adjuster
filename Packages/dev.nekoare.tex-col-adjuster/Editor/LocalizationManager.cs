using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TexColAdjuster
{
    public enum Language
    {
        English,
        Japanese
    }
    
    public static class LocalizationManager
    {
        private static Language currentLanguage = Language.Japanese;
        private static Dictionary<string, Dictionary<Language, string>> localizedStrings;
        
        private const string LANGUAGE_PREF_KEY = "TexColAdjuster_Language";
        
        static LocalizationManager()
        {
            InitializeLocalizedStrings();
            LoadLanguagePreference();
        }
        
        public static Language CurrentLanguage
        {
            get => currentLanguage;
            set
            {
                currentLanguage = value;
                SaveLanguagePreference();
            }
        }
        
        public static string Get(string key)
        {
            if (localizedStrings.TryGetValue(key, out var translations))
            {
                if (translations.TryGetValue(currentLanguage, out var translation))
                {
                    return translation;
                }
            }
            
            Debug.LogWarning($"Localization key '{key}' not found for language '{currentLanguage}'");
            return key;
        }
        
        private static void InitializeLocalizedStrings()
        {
            localizedStrings = new Dictionary<string, Dictionary<Language, string>>();
            
            // Window Title
            AddLocalization("window_title", "TexColAdjuster", "TexColAdjuster");
            AddLocalization("window_subtitle", "VRChatアバター用テクスチャ色合わせツール", "VRChat Avatar Texture Color Adjuster");
            AddLocalization("window_description", "高度なテクスチャ色調整ツール", "Advanced texture color matching tool");
            
            // Language Toggle
            AddLocalization("language_toggle", "言語 / Language", "Language / 言語");
            
            // Tabs
            AddLocalization("tab_basic", "基本", "Basic");
            AddLocalization("tab_color_adjust", "色調整", "Color Adjust");
            AddLocalization("tab_shader_settings", "シェーダー設定転送", "Shader Settings Transfer");
            AddLocalization("tab_direct", "直接指定", "Direct");
            
            // Texture Selection
            AddLocalization("texture_selection", "テクスチャ選択", "Texture Selection");
            AddLocalization("reference_texture", "参考にしたい色:", "Desired Color (Reference):");
            AddLocalization("target_texture", "色を変えたいテクスチャ:", "Texture to Adjust:");
            
            // Adjustment Parameters
            AddLocalization("adjustment_parameters", "調整パラメータ", "Adjustment Parameters");
            AddLocalization("adjustment_intensity", "調整強度", "Adjustment Intensity");
            AddLocalization("preserve_luminance", "輝度を保持", "Preserve Luminance");
            AddLocalization("preserve_texture", "テクスチャ品質を保持", "Preserve Texture Quality");
            AddLocalization("adjustment_mode", "調整モード", "Adjustment Mode");
            
            // Preview Controls
            AddLocalization("preview", "プレビュー", "Preview");
            AddLocalization("show_preview", "プレビューを表示", "Show Preview");
            AddLocalization("realtime_preview", "リアルタイムプレビュー", "Real-time Preview");
            AddLocalization("original", "元画像", "Original");
            AddLocalization("adjusted", "調整後", "Adjusted");
            
            // Action Buttons
            AddLocalization("generate_preview", "再生成", "Generate Preview");
            AddLocalization("apply_adjustment", "調整を適用", "Apply Adjustment");
            
            // Processing
            AddLocalization("processing", "処理中...", "Processing...");
            AddLocalization("processing_status", "処理: {0}", "Processing: {0}");
            
            // Settings & Presets
            AddLocalization("settings_presets", "設定とプリセット", "Settings & Presets");
            AddLocalization("presets", "プリセット", "Presets");
            AddLocalization("load_preset", "プリセット読み込み", "Load Preset");
            AddLocalization("load", "読み込み", "Load");
            AddLocalization("delete", "削除", "Delete");
            AddLocalization("save_current_preset", "現在の設定をプリセットに保存", "Save Current as Preset");
            
            // Preset Names
            AddLocalization("preset_anime", "アニメ調", "Anime Style");
            AddLocalization("preset_realistic", "リアル調", "Realistic");
            AddLocalization("preset_soft", "ソフト調整", "Soft Adjustment");
            
            // Adjustment Modes
            AddLocalization("mode_lab_histogram", "LABヒストグラムマッチング", "Lab Histogram Matching");
            AddLocalization("mode_hue_shift", "色相シフト", "Hue Shift");
            AddLocalization("mode_color_transfer", "色調転送", "Color Transfer");
            AddLocalization("mode_adaptive", "適応的調整", "Adaptive Adjustment");
            
            // Dialog Messages
            AddLocalization("no_preview_title", "プレビューなし", "No Preview");
            AddLocalization("no_preview_message", "最初にプレビューを生成してください。", "Please generate a preview first.");
            AddLocalization("apply_adjustment_title", "調整を適用", "Apply Adjustment");
            AddLocalization("apply_adjustment_message", "調整したテクスチャを保存しますか？", "Save the adjusted texture?");
            AddLocalization("save_as_new", "新しいファイルとして保存", "Save As New File");
            AddLocalization("overwrite_original", "元ファイルを上書き", "Overwrite Original");
            AddLocalization("success_title", "成功", "Success");
            AddLocalization("success_message", "テクスチャの調整が正常に適用されました！", "Texture adjustment applied successfully!");
            AddLocalization("error_title", "エラー", "Error");
            AddLocalization("error_save_message", "調整したテクスチャの保存に失敗しました。", "Failed to save the adjusted texture.");
            AddLocalization("error_preview_message", "プレビューの生成に失敗しました: {0}", "Failed to generate preview: {0}");
            AddLocalization("ok", "OK", "OK");
            AddLocalization("cancel", "キャンセル", "Cancel");
            AddLocalization("yes", "はい", "Yes");
            
            // Input Dialog
            AddLocalization("save_preset_title", "プリセットを保存", "Save Preset");
            AddLocalization("save_preset_message", "プリセット名を入力してください:", "Enter preset name:");
            AddLocalization("new_preset_default", "新しいプリセット", "New Preset");
            
            
            // Color Selection Mode
            AddLocalization("color_selection_mode", "スポイト機能", "Eyedropper Tool");
            AddLocalization("enable_color_selection", "スポイトで色選択", "Pick Color with Eyedropper");
            AddLocalization("color_selection_help", "白黒や複数色が混在するテクスチャで、特定の色のみを参考にしたい場合に有効化してください。テクスチャ上をクリックして色を選択できます。", "Enable when you want to pick a specific color from textures with distinct colors. Click on the texture to select a color.");
            AddLocalization("dual_color_selection_help", "両方のテクスチャから色を選択して、指定した色同士をマッチングします。左側で変更したい色、右側で目標となる色をクリックしてください。", "Select colors from both textures to match them together. Click the color to change on the left, and the target color on the right.");
            AddLocalization("eyedropper_active", "スポイトモード有効", "Eyedropper Mode Active");
            AddLocalization("click_on_texture", "テクスチャ上をクリックして色を選択", "Click on texture to pick color");
            AddLocalization("selected_color", "選択色:", "Selected:");
            AddLocalization("hover_color", "マウス位置:", "Hover:");
            AddLocalization("eyedropper_instructions", "テクスチャ上をマウスオーバーして色を確認し、クリックで選択してください", "Hover over texture to preview color, click to select");
            AddLocalization("color_selection_instructions", "両方のテクスチャから色をクリックして選択し、色をマッチングします", "Click on colors in both textures to match them together");
            AddLocalization("color_selection_guide", "左: 変更したい色 | 右: 目標となる色", "Left: Target texture (color to change) | Right: Reference texture (desired color)");
            AddLocalization("target_color", "変更対象色", "Target Color");
            AddLocalization("reference_color", "参照色", "Reference Color");
            AddLocalization("color_selection_range", "色選択範囲", "Color Selection Range");
            AddLocalization("selection_range", "選択範囲", "Selection Range");
            AddLocalization("selection_range_help", "低い値 = より精密な選択、高い値 = より広範囲の選択", "Lower values = more precise selection, Higher values = broader selection");
            AddLocalization("colors_selected_ready", "✓ 両方の色が選択されました - 色マッチング適用可能", "✓ Both colors selected - ready to apply color matching");
            AddLocalization("select_both_colors", "両方のテクスチャから色を選択してください", "Please select colors from both textures");
            
            // Color Adjustments
            AddLocalization("color_adjustments", "色調整", "Color Adjustments");
            AddLocalization("adjustment_target", "調整対象", "Adjustment Target");
            AddLocalization("target_texture_mode", "色を変えたいテクスチャ", "Target Texture");
            AddLocalization("reference_color_mode", "参考にしたい色", "Reference Color");
            AddLocalization("gamma_adjustment", "ガンマ", "Gamma");
            AddLocalization("saturation_adjustment", "彩度", "Saturation");
            AddLocalization("brightness_adjustment", "明度", "Brightness");
            AddLocalization("reset_adjustments", "リセット", "Reset");
            
            // Single Texture Adjustment
            AddLocalization("single_texture_color_adjust", "単一テクスチャ色調整", "Single Texture Color Adjustment");
            AddLocalization("select_texture", "テクスチャを選択:", "Select Texture:");
            AddLocalization("help_single_texture", "ガンマ、彩度、明度を調整するテクスチャを選択してください", "Select texture to adjust gamma, saturation, and brightness");
            
            // Help Text
            AddLocalization("help_reference_texture", "色調の基準となるテクスチャを選択してください", "Select the texture to use as color reference");
            AddLocalization("help_target_texture", "色調を調整したいテクスチャを選択してください", "Select the texture to adjust");
            AddLocalization("help_adjustment_intensity", "調整の強さを設定します (0-100%)", "Set the adjustment intensity (0-100%)");
            AddLocalization("help_preserve_luminance", "元の明暗情報を保持します", "Preserve original brightness information");
            AddLocalization("help_realtime_preview", "パラメータ変更時に自動でプレビューを更新します", "Automatically update preview when parameters change");
            
            // Liltoon Preset Management
            AddLocalization("liltoon_presets", "liltoonプリセット", "Liltoon Presets");
            AddLocalization("preset_management", "プリセット管理", "Preset Management");
            AddLocalization("material_selection", "マテリアル選択", "Material Selection");
            AddLocalization("target_materials", "対象マテリアル:", "Target Materials:");
            AddLocalization("preset_selection", "プリセット選択", "Preset Selection");
            AddLocalization("select_preset", "プリセットを選択:", "Select Preset:");
            AddLocalization("no_preset_selected", "プリセットが選択されていません", "No preset selected");
            AddLocalization("load_from_material", "マテリアルから読み込み", "Load from Material");
            AddLocalization("save_preset", "プリセット保存", "Save Preset");
            AddLocalization("apply_intensity", "適用強度", "Apply Intensity");
            AddLocalization("apply_flags", "適用設定", "Apply Settings");
            AddLocalization("main_settings", "基本設定", "Main Settings");
            AddLocalization("shadow_settings", "影設定", "Shadow Settings");
            AddLocalization("shadow2nd_settings", "2影設定", "2nd Shadow");
            AddLocalization("rim_settings", "リム設定", "Rim Settings");
            AddLocalization("backlight_settings", "逆光設定", "Backlight");
            AddLocalization("emission_settings", "発光設定", "Emission");
            AddLocalization("outline_settings", "アウトライン設定", "Outline");
            AddLocalization("srimshade_settings", "sRimShade設定", "sRimShade");
            AddLocalization("lighting_settings", "ライティング設定", "Lighting Settings");
            AddLocalization("rendering_settings", "レンダリング設定", "Rendering Settings");
            AddLocalization("apply_preset", "プリセット適用", "Apply Preset");
            AddLocalization("apply_to_selected", "選択マテリアルに適用", "Apply to Selected");
            AddLocalization("apply_to_all", "全liltoonマテリアルに適用", "Apply to All Liltoon");
            AddLocalization("generate_from_material", "マテリアルからプリセット生成", "Generate from Material");
            AddLocalization("no_liltoon_materials", "liltoonマテリアルが見つかりません", "No liltoon materials found");
            AddLocalization("no_presets_found", "プリセットが見つかりません", "No presets found");
            AddLocalization("material_not_liltoon", "選択されたマテリアルはliltoonではありません", "Selected material is not liltoon");
            AddLocalization("preset_applied_success", "プリセットを適用しました: {0}", "Preset applied: {0}");
            AddLocalization("preset_saved_success", "プリセットを保存しました: {0}", "Preset saved: {0}");
            AddLocalization("select_material_first", "まずマテリアルを選択してください", "Please select a material first");
            AddLocalization("enter_preset_name", "プリセット名を入力:", "Enter preset name:");
            AddLocalization("new_liltoon_preset", "新しいliltoonプリセット", "New Liltoon Preset");
            
            // Material Transfer Workflow
            AddLocalization("material_transfer", "マテリアル設定転送", "Material Settings Transfer");
            AddLocalization("source_material", "転送元マテリアル:", "Source Material:");
            AddLocalization("target_material", "転送先マテリアル:", "Target Material:");
            AddLocalization("transfer_intensity", "転送強度", "Transfer Intensity");
            AddLocalization("transfer_settings", "設定転送", "Transfer Settings");
            AddLocalization("drawing_effects_only", "転送", "Transfer Drawing Effects Only");
            AddLocalization("drawing_effects_help", "ライティング・影・sRimShade・逆光・リムの設定を転送します", "Transfers Lighting, Shadow, sRimShade, Backlight, and Rim settings");
            AddLocalization("transfer_success", "設定転送が完了しました", "Settings transfer completed");
            AddLocalization("select_both_materials", "両方のマテリアルを選択してください", "Please select both materials");
            AddLocalization("materials_must_be_liltoon", "両方のマテリアルがliltoonである必要があります", "Both materials must be liltoon");
        }
        
        private static void AddLocalization(string key, string japanese, string english)
        {
            if (!localizedStrings.ContainsKey(key))
            {
                localizedStrings[key] = new Dictionary<Language, string>();
            }
            
            localizedStrings[key][Language.Japanese] = japanese;
            localizedStrings[key][Language.English] = english;
        }
        
        private static void SaveLanguagePreference()
        {
            EditorPrefs.SetInt(LANGUAGE_PREF_KEY, (int)currentLanguage);
        }
        
        private static void LoadLanguagePreference()
        {
            if (EditorPrefs.HasKey(LANGUAGE_PREF_KEY))
            {
                currentLanguage = (Language)EditorPrefs.GetInt(LANGUAGE_PREF_KEY);
            }
        }
        
        public static string GetFormattedString(string key, params object[] args)
        {
            string format = Get(key);
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }
        
        public static void ToggleLanguage()
        {
            CurrentLanguage = CurrentLanguage == Language.Japanese ? Language.English : Language.Japanese;
        }
        
        public static string GetLanguageDisplayName()
        {
            return CurrentLanguage == Language.Japanese ? "日本語" : "English";
        }
        
        public static string GetColorAdjustmentModeDisplayName(ColorAdjustmentMode mode)
        {
            switch (mode)
            {
                case ColorAdjustmentMode.LabHistogramMatching:
                    return Get("mode_lab_histogram");
                case ColorAdjustmentMode.HueShift:
                    return Get("mode_hue_shift");
                case ColorAdjustmentMode.ColorTransfer:
                    return Get("mode_color_transfer");
                case ColorAdjustmentMode.AdaptiveAdjustment:
                    return Get("mode_adaptive");
                default:
                    return mode.ToString();
            }
        }
        
        public static string[] GetColorAdjustmentModeDisplayNames()
        {
            return new string[]
            {
                GetColorAdjustmentModeDisplayName(ColorAdjustmentMode.LabHistogramMatching),
                GetColorAdjustmentModeDisplayName(ColorAdjustmentMode.HueShift),
                GetColorAdjustmentModeDisplayName(ColorAdjustmentMode.ColorTransfer),
                GetColorAdjustmentModeDisplayName(ColorAdjustmentMode.AdaptiveAdjustment)
            };
        }
    }
}