# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## 更新履歴

### 2.0.0 - 2026-03-20

#### 新機能
- 新UI（ステップ方式）を追加。旧UIとトグルで切り替え可能
  - Basic / Direct / シェーダー設定転送の3タブすべてに対応
  - 未完了ステップのグレーアウト表示
- サムネイル付きマテリアル選択UI
- ウィンドウ内プレビュー（参照テクスチャ + 調整後テクスチャの横並び表示）
- 調整スライダー追加: 明るさ、鮮やかさ、ガンマ、色合いシフト
- 調整スライダーのリアルタイムプレビュー反映（LABマッチング結果キャッシュ方式）
- スポイト色選択の2段階処理（LABマッチング → DualSelection補正）
- シェーダー設定転送: ドロップ追加式の転送先選択
- シェーダー設定転送: Material / GameObject 自動判別入力
- 最後に開いたタブの記憶

#### 改善
- NDMFコンポーネントに色合いシフト（midtoneShift）スライダー追加
- NDMFの brightness を加算方式に統一（ウィンドウと同じ操作感）
- NDMFビルド時の MipStreaming 有効化
- NDMFビルド時に非圧縮テクスチャを使用（ブロックノイズ軽減）
- 影設定転送に Shadow 2nd / 3rd を追加
- 影設定転送でテクスチャが誤転送される問題を修正
- 転送項目の「sRimShade」表記を「リムシェード」に変更
- 調整を適用で生成されるテクスチャの MipStreaming 有効化
- GPU Apply パスの追加（プレビューと同じGPUパイプラインで適用）
- CPU / GPU 間の色空間処理の統一

#### 破壊的変更
- NDMFコンポーネントの `brightness` パラメータが乗算方式（デフォルト1）から加算方式（デフォルト0）に変更
  - 自動マイグレーション対応: 旧バージョンのシーンは自動変換されます

### 1.3.1 - 2025-12-04
- テクスチャのインポート設定を保持するように修正
  - テクスチャ処理後にインポート設定が変更されない仕様に改善
  - 元のテクスチャのインポート設定が維持されるよう対応

### 1.3.0 - 2025-11-16
- NDMFサポートの追加
  - NDMF統合によるビルド時の自動テクスチャ調整
  - TextureColorAdjustmentComponentの追加
  - ビルド時プレビュー機能
  - 自動クリーンアップ機能
- Runtimeフォルダの追加とRuntime Assembly Definition対応
- VRChatアバター向けNDMFワークフローのサポート
- vpmDependenciesにnadena.dev.ndmf (>=1.4.0)を追加

### 1.2
- シェーダー設定転送タブのMaterialUnifyTool同等機能を完成
- コンパイルエラーの解消
- UIの安定化
- 12個のカテゴリ選択機能（Lighting、Shadow、Emission、sRimShade、Backlight、Reflection、MatCap、MatCap 2nd、Rim Light、Distance Fade、Outline等）
- テクスチャ転送オプションの追加（Reflection CubeMap、MatCap Texture、MatCap Bump Mask）
- ゲームオブジェクト選択ベースのワークフロー

### 1.0.3
- MaterialUnifyToolとの競合問題を修正
- シェーダー設定転送機能でMaterialUnifyTool同等の機能を実装
- 変数名の競合を回避するためのリネーム対応

### 1.0.2
- シェーダー設定転送機能の追加
- Material Unify Tool機能の統合

### 1.0.0
- Texture Color Adjuster の初回リリース
- VCCリポジトリ対応
- 高度な色調整機能の実装
- 日本語・英語対応

---

## [1.0.0] - 2025-01-18

### Added
- Initial release of Texture Color Adjuster
- Advanced color adjustment algorithms:
  - LAB Histogram Matching
  - Hue Shift Adjustment
  - Color Transfer
  - Adaptive Adjustment
- Dual Color Selection mode for precise color matching
- Luminance preservation option
- Real-time preview functionality
- Texture export capabilities
- Multiple color space support (RGB, HSV, LAB)
- Comprehensive GUI with intuitive controls
- Localization support (Japanese/English)

### Features
- **Color Processing**: Advanced color manipulation using perceptually uniform LAB color space
- **Flexible Modes**: Multiple adjustment modes for different use cases
- **Precision Control**: Fine-grained control over adjustment intensity and range
- **User-Friendly Interface**: Intuitive Unity Editor window with real-time feedback
- **Export Options**: Save adjusted textures in various formats

### Technical
- Unity 2022.3+ compatibility
- Optimized texture processing algorithms
- Safe texture handling with proper memory management
- Cross-platform support (Windows, macOS, Linux)

### Dependencies
- Unity 2022.3 or later
- No external dependencies

---

## Development Notes

This project was developed to address the need for advanced color adjustment tools in Unity. The implementation focuses on perceptually accurate color processing using the LAB color space, which provides better results than traditional RGB-based adjustments.

### Key Design Decisions
- LAB color space for perceptually uniform color processing
- Modular architecture for easy extension of adjustment algorithms
- Safe texture handling to prevent memory issues
- Comprehensive error handling and user feedback

### Future Enhancements
- Additional color adjustment algorithms
- Batch processing capabilities
- Custom color space support
- Advanced masking features
