# リリースチェックリスト

このドキュメントは、Texture Color Adjuster をアップデートする際に必要な作業のチェックリストです。

## 📋 リリース前の準備

Editorフォルダ内のファイルに変更・追加が入った場合、以下の手順でリリースを実施してください。

---

## ✅ 必須作業

### 1. バージョン番号の更新

- [ ] **`Packages/dev.nekoare.tex-col-adjuster/package.json`** のバージョン番号を更新
  - セマンティックバージョニング（[SemVer](https://semver.org/lang/ja/)）に従う
  - **MAJOR.MINOR.PATCH** 形式（例: `1.3.0`）
  - バージョン番号の意味:
    - **MAJOR**: 互換性のない API 変更
    - **MINOR**: 後方互換性のある機能追加
    - **PATCH**: 後方互換性のあるバグ修正
  - 例：
    ```json
    {
      "version": "1.3.0"
    }
    ```

### 2. 変更履歴の更新

- [ ] **`CHANGELOG.md`** に新バージョンの変更内容を追加
  - バージョン番号と日付を記載
  - 変更内容を分類して記載:
    - **追加 (Added)**: 新機能
    - **変更 (Changed)**: 既存機能の変更
    - **非推奨 (Deprecated)**: 今後削除される機能
    - **削除 (Removed)**: 削除された機能
    - **修正 (Fixed)**: バグ修正
    - **セキュリティ (Security)**: セキュリティ修正
  - 例：
    ```markdown
    ### 1.3.0 - 2025-XX-XX

    #### 追加
    - 新しい色調整アルゴリズムの追加
    - バッチ処理機能の実装

    #### 修正
    - テクスチャ保存時のメモリリーク問題を修正
    ```

### 3. ドキュメントの更新

- [ ] **`README.md`** の更新（必要に応じて）
  - 新機能の説明を追加
  - スクリーンショットやサンプルコードの更新
  - 使い方セクションの更新

- [ ] **`Packages/dev.nekoare.tex-col-adjuster/README.md`** の更新（必要に応じて）
  - パッケージ内のREADMEも同期更新

### 4. コードの品質チェック

- [ ] **コンパイルエラーがないことを確認**
  - Unity Editorでプロジェクトを開いて確認
  - コンソールにエラーが出ていないか確認

- [ ] **機能テスト**
  - 主要機能が正常に動作することを確認
  - 新規追加機能のテスト
  - 既存機能のリグレッションテスト

- [ ] **互換性確認**
  - Unity 2022.3以上での動作確認
  - VRChat SDK との互換性確認（vpmDependenciesを確認）

### 5. Git コミット

- [ ] **変更をコミット**
  ```bash
  git add Packages/dev.nekoare.tex-col-adjuster/package.json
  git add CHANGELOG.md
  git add README.md  # (必要に応じて)
  git add Packages/dev.nekoare.tex-col-adjuster/Editor/  # (変更されたファイル)
  git commit -m "Release v1.3.0: [変更内容の要約]"
  ```

### 6. メインブランチへのプッシュ

- [ ] **main または master ブランチにプッシュ**
  ```bash
  git push origin main
  # または
  git push origin master
  ```

  ⚠️ **重要**: mainまたはmasterブランチへのプッシュにより、GitHub Actionsワークフローが自動的に実行されます。

---

## 🤖 自動実行される処理（GitHub Actions）

以下の処理は `.github/workflows/pages.yml` により自動的に実行されます：

- ✅ `package.json` からバージョン番号を読み取り
- ✅ バージョン番号のSemVer正規化（例: `1.2` → `1.2.0`）
- ✅ パッケージのZIPファイル作成（`dev.nekoare.tex-col-adjuster-v{version}.zip`）
- ✅ ZIP ファイルの SHA256 ハッシュ計算
- ✅ `index.json` への新バージョンエントリの追加
- ✅ GitHub Pages へのデプロイ（VCCリポジトリの公開）

**確認事項:**
- [ ] GitHub Actions ワークフローが正常に完了したことを確認
  - リポジトリの "Actions" タブでワークフローの実行状況を確認
  - すべてのステップが緑色（成功）になっていることを確認

- [ ] GitHub Pages が更新されたことを確認
  - `https://nekoare.github.io/texture-color-adjuster/index.json` にアクセス
  - 新しいバージョンが追加されていることを確認

---

## 📦 オプション作業

### 7. GitHub Release の作成（推奨）

ユーザーへの可視性を高めるため、GitHub Release を作成することを推奨します。

- [ ] **GitHub Releasesページで新しいリリースを作成**
  1. リポジトリの "Releases" ページに移動
  2. "Draft a new release" をクリック
  3. タグを作成（例: `v1.3.0`）
  4. リリースタイトルを入力（例: `Release v1.3.0`）
  5. リリースノートを記載（CHANGELOG.mdの内容をコピー）
  6. 生成されたZIPファイルを添付（オプション）
  7. "Publish release" をクリック

- [ ] **リリースノートの内容確認**
  - CHANGELOG.md の内容と一致していることを確認
  - 日本語と英語の両方で記載することを推奨

### 8. コミュニティへの通知

- [ ] **Discord/Twitterなどでリリースを告知**（必要に応じて）
  - 新機能のハイライト
  - リリースページへのリンク

---

## 🔍 トラブルシューティング

### GitHub Actions ワークフローが失敗した場合

1. **エラーログを確認**
   - "Actions" タブから失敗したワークフローをクリック
   - どのステップで失敗したか確認

2. **よくある原因:**
   - `package.json` の JSON 形式が不正
   - `index.json` の JSON 形式が不正
   - ファイルパスの誤り
   - 権限の問題

3. **修正後の再実行:**
   - 問題を修正してコミット＆プッシュ
   - ワークフローが自動的に再実行される

### バージョン番号の問題

- **package.json では `1.2` と記載しても問題ありません**
  - GitHub Actions が自動的に `1.2.0` に正規化します
  - ただし、明示的に `1.2.0` と記載することを推奨

### index.json が更新されない

- GitHub Pages のデプロイには数分かかる場合があります
- 5-10分待ってから再度確認してください
- キャッシュの問題の場合は、ブラウザのキャッシュをクリア

---

## 📝 チェックリスト要約

リリース時には以下の順序で作業を実施してください：

1. ✅ package.json のバージョン番号更新
2. ✅ CHANGELOG.md の更新
3. ✅ README.md の更新（必要に応じて）
4. ✅ コンパイルエラーのチェック
5. ✅ 機能テストの実施
6. ✅ Git コミット
7. ✅ main/master ブランチへプッシュ
8. ✅ GitHub Actions ワークフローの完了確認
9. ✅ GitHub Pages の更新確認
10. ✅ GitHub Release の作成（オプション）
11. ✅ コミュニティへの通知（オプション）

---

## 📚 参考リンク

- [Semantic Versioning](https://semver.org/lang/ja/)
- [Keep a Changelog](https://keepachangelog.com/ja/1.0.0/)
- [VCC Package Repository](https://vcc.docs.vrchat.com/vpm/repos/)
- [GitHub Actions Documentation](https://docs.github.com/ja/actions)

---

**最終更新**: 2025-11-16
