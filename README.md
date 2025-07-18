# Texture Color Adjuster

![Unity](https://img.shields.io/badge/Unity-2022.3+-blue.svg)
![VRChat SDK](https://img.shields.io/badge/VRChat%20SDK-3.7.0+-green.svg)
![License](https://img.shields.io/badge/License-MIT-yellow.svg)

Texture Color Adjuster は、Unity Editor上でテクスチャの色調整を行うための高度なツールです。様々な色空間をサポートし、インテリジェントな色操作アルゴリズムを提供します。

## 特徴

### 🎨 高度な色調整
- LAB色空間での精密な色調整
- RGB、HSV、LAB色空間での変換・調整
- リアルタイムプレビュー機能
- バッチ処理対応

### 🖼️ テクスチャ処理
- 複数テクスチャの同時処理
- 元画像を保持したまま調整
- 高品質な色変換アルゴリズム
- メモリ効率の最適化

### 🔧 Unity統合
- 直感的なエディタウィンドウ
- アンドゥ・リドゥ対応
- プロジェクトブラウザ統合
- カスタムインスペクタ

## インストール方法

### VCC (ALCOM) を使用する場合

1. VCC (ALCOM) を開く
2. 「Settings」→「Packages」→「Add Repository」をクリック
3. 以下のURLを入力:
   ```
   https://nekoare.github.io/texture-color-adjuster/index.json
   ```
4. 「I Understand, Add Repository」をクリック
5. プロジェクトの「Packages」タブから「Texture Color Adjuster」を追加

### 手動インストール

1. [Releases](https://github.com/nekoare/texture-color-adjuster/releases) から最新版をダウンロード
2. UnityPackageファイルをプロジェクトにインポート

## 使い方

### 基本的な使用方法

1. Unity Editor で `Tools` → `Texture Color Adjuster` を選択
2. 調整したいテクスチャを選択
3. 色調整パラメータを調整
4. 「Apply」ボタンで変更を適用

### 色空間変換

1. 「Color Space」ドロップダウンから変換先を選択
2. 各パラメータを調整
3. プレビューで結果を確認
4. 満足したら「Export」で出力

## 必要な環境

- Unity: 2022.3 以上
- VRChat SDK: 3.7.0 以上（VRChatプロジェクトで使用する場合）

## トラブルシューティング

### よくある問題

**Q: テクスチャが正しく読み込めない**
A: テクスチャの読み込み設定を確認し、「Read/Write Enabled」がオンになっているか確認してください。

**Q: 色調整の結果が期待と異なる**
A: 使用している色空間とガンマ補正の設定を確認してください。

**Q: メモリ不足エラーが発生する**
A: 大きなテクスチャを処理する際は、バッチサイズを小さくして処理してください。

## 貢献

プルリクエストやIssueの報告を歓迎します。

## ライセンス

MIT License - 詳細は [LICENSE](LICENSE) ファイルを参照してください。

## 更新履歴

詳細な更新履歴は [CHANGELOG.md](CHANGELOG.md) を参照してください。

## 作者

- nekoare - [GitHub](https://github.com/nekoare)

## 謝辞

- Unity コミュニティ
- VRChat コミュニティ
