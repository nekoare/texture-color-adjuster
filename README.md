# Texture Color Adjuster

![Unity](https://img.shields.io/badge/Unity-2022.3+-blue.svg)
![License](https://img.shields.io/badge/License-MIT-green.svg)

Texture Color Adjuster は、Unity Editor で高度なテクスチャ色調整を行うための拡張機能です。複数の色空間をサポートし、知的な色操作アルゴリズムを提供します。

## 特徴

### 🎨 高度な色調整機能
- **LAB ヒストグラムマッチング**: 知覚的に正確な色調整
- **色相シフト**: HSV色空間での色相調整
- **色転移**: 統計的な色特性の転移
- **適応的調整**: 複数の手法を組み合わせた高度な調整

### 🔧 柔軟な操作モード
- **デュアルカラー選択**: 特定の色を選択して置換
- **メインカラー指定**: 主要色を基準とした調整
- **輝度保持**: 明度を保持した色調整
- **強度調整**: 調整効果の強度制御

### 📱 ユーザビリティ
- **直感的なGUI**: 使いやすいエディタウィンドウ
- **リアルタイムプレビュー**: 調整結果の即座確認
- **テクスチャエクスポート**: 調整結果の保存機能
- **多様なフォーマット対応**: 様々な画像形式に対応

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

1. Unity Editor で「Tools」→「Texture Color Adjuster」を開く
2. 調整したいテクスチャと参照テクスチャを選択
3. 調整モードを選択（LAB ヒストグラムマッチング、色相シフト等）
4. 強度スライダーで調整効果を調整
5. 「Apply」ボタンで適用、「Export」ボタンで保存

### 高度な機能

#### デュアルカラー選択
特定の色を選択して、別の色に置換する機能です。

1. 「Dual Color Selection」モードを選択
2. ターゲットカラーと参照カラーを指定
3. 選択範囲スライダーで影響範囲を調整
4. 調整を適用

#### 輝度保持モード
明度を保持しながら色調のみを調整する機能です。

1. 「Preserve Luminance」チェックボックスを有効化
2. 通常の調整を実行
3. 元の明度を保持した状態で色調が調整されます

## 必要な環境

- **Unity**: 2022.3 以上
- **対応OS**: Windows 10/11, macOS 10.14+, Linux

## 技術仕様

### サポートされる色空間
- RGB
- HSV
- LAB (CIE L*a*b*)

### 調整アルゴリズム
- LAB ヒストグラムマッチング
- 色相シフト調整
- 色転移調整
- 適応的調整

### 対応フォーマット
- PNG
- JPEG
- TGA
- EXR
- その他Unity対応フォーマット

## 貢献

プロジェクトへの貢献を歓迎します！

1. フォークしてください
2. フィーチャーブランチを作成してください (`git checkout -b feature/AmazingFeature`)
3. 変更をコミットしてください (`git commit -m 'Add some AmazingFeature'`)
4. ブランチにプッシュしてください (`git push origin feature/AmazingFeature`)
5. プルリクエストを作成してください

## ライセンス

MIT License - 詳細は [LICENSE](./LICENSE) ファイルを参照してください。

## 更新履歴

詳細な更新履歴は [CHANGELOG.md](./CHANGELOG.md) を参照してください。

## 作者

**nekoare**
- GitHub: [@nekoare](https://github.com/nekoare)

## 謝辞

このプロジェクトは、高度な色理論とUnityエディタ拡張の技術を組み合わせて作成されました。色空間変換アルゴリズムの実装にあたり、様々な学術的資料を参考にさせていただきました。
