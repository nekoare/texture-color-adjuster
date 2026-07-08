# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## 更新履歴

### 2.0.4 - 2026-07-08

#### 修正
- 「テクスチャで指定」で明るさ・鮮やかさ・ガンマ・色合いシフトを調整しても、「調整を適用」で保存されるテクスチャに反映されない問題を修正（従来はプレビューにのみ反映されていました）
- 「パーツで指定」の「調整を適用」（NDMFを使用しない場合）で、GPU処理が使えない環境、または調整モードがLABヒストグラムマッチング以外のときに、明るさ等のポスト調整とスポイト補正が適用されない問題を修正

#### 改善
- 「テクスチャで指定」の適用処理をプレビューと同じGPUパイプラインに統一し、プレビューと保存結果が一致するように変更
- 明るさ・鮮やかさ・ガンマ・色合いシフトの処理を共通化（プレビューと適用で必ず同じ計算式を使用）
- 処理中に生成する一時テクスチャの解放漏れを修正

### 2.0.3 - 2026-07-07

#### 新機能
- パーツで指定タブに「適用済みパーツ」一覧を追加（適用済みの各パーツを一覧表示し、編集・削除が可能）。複数パーツにそれぞれ別の色を割り当てる運用がしやすくなりました

#### 改善
- 「パーツごとに適用」を連続適用向けに改善（適用ごとの完了ダイアログを廃止し、ウィンドウ内インライン表示に変更。適用後も選択を維持）
- 「全体に適用」で、対象マテリアルが複数パーツに共有されている場合に確認ダイアログを表示（全パーツが同じ色になる旨を警告）
- ウィンドウ内プレビューでテクスチャの一時再インポートを廃止し、パーツ切り替え時の動作を軽量化。あわせてlilToonの `.lilblock` 警告が多発する問題を解消

#### 修正
- 「全体に適用」後に「パーツごとに適用」しても、そのパーツが全体の色のままになる問題を修正（パーツごとの適用が全体設定を確実に上書きするように変更）

### 2.0.2 - 2026-04-08

#### 修正
- NDMFプレビュー/ビルドでUVマスクが統計計算に使用されず、ウィンドウプレビューと色が大幅にずれる問題を修正
- 「全体に適用」ボタンでポスト調整値（色合いシフト・鮮やかさ・明るさ・ガンマ）がデフォルト値にリセットされる問題を修正
- 「全体に適用」でmidtoneShiftパラメータがNDMFコンポーネントに渡されない問題を修正
- NDMFプレビューキャッシュ更新時にCubemapプロパティへの2Dテクスチャ代入エラーが発生する問題を修正

#### 改善
- NDMFモードで「見え方も転送（マテリアル設定の転送）」を使用した場合、マテリアル設定を破壊的に適用するように変更（NDMFプレビューとの不一致を解消）
- CPUパスのLABヒストグラムマッチングにUVマスクフィルタリングを追加
- UVマスクサイズ不一致時の警告ログを追加

### 2.0.1 - 2026-03-20

#### 改善
- UVマスクによるLAB統計フィルタリング（テクスチャの未使用領域を統計から除外）
- ウィンドウ内プレビューにUV領域表示オーバーレイを追加
- LABヒストグラムマッチング以外の調整モードでも調整スライダーのリアルタイム反映に対応
- CPUパスの調整結果キャッシュ追加（スライダー変更時の即時反映）

#### 修正
- CPUパスでポスト調整（明るさ、鮮やかさ、ガンマ、色合いシフト）が適用されない問題を修正
- UVマスクオーバーレイの上下反転を修正
- UVマスクオーバーレイのアルファブレンド修正

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
