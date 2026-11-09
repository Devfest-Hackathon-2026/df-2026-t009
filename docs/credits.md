# 使用した素材・ライブラリ

## サービス

| 名前 | 用途 |
| --- | --- |
| Google Maps Platform：Map Tiles API（Photorealistic 3D Tiles） | 立体地図（アプリ本体）。地図の表記（Google と提供元）は画面に表示している |
| Google Maps Platform：Maps JavaScript API | 軽い版の地図 |
| Firebase（Authentication / Cloud Firestore / Cloud Storage / Cloud Functions / Hosting） | ログイン、データ、写真、サーバーの判定、公開 |

## アプリ本体（Unity）

| 名前 | ライセンス | 用途 |
| --- | --- | --- |
| Unity 6（6000.3） | Unity の利用規約 | アプリ本体の開発と書き出し |
| Universal Render Pipeline（com.unity.render-pipelines.universal） | Unity Companion License | 描画と画面効果 |
| UI Toolkit（com.unity.modules.uielements） / uGUI（com.unity.ugui） | Unity Companion License | 画面と入力 |
| Cesium for Unity（com.cesium.unity） | Apache-2.0 | 立体地図の読み込みと座標の変換 |
| Unity glTFast（com.unity.cloud.gltfast） | Apache-2.0 | アバターの 3D モデル（glb）の読み込み |

- 読み込み画面は自作で、Unity のロゴは表示していない。

## サーバー・軽い版（ウェブ）

| 名前 | ライセンス | 用途 |
| --- | --- | --- |
| firebase-admin | Apache-2.0 | Cloud Functions からのデータ操作 |
| firebase-functions | MIT | Cloud Functions |
| firebase-tools | MIT | エミュレーターとデプロイ |
| TypeScript | Apache-2.0 | 型チェック |
| React / React DOM | MIT | 軽い版の画面 |
| Vite | MIT | 軽い版のビルド |
| @vis.gl/react-google-maps | MIT | 軽い版で Google Maps を使う |
| firebase（JavaScript SDK） | Apache-2.0 | 軽い版の通信 |
| @capacitor/core・@capacitor/android・@capacitor/cli | MIT | 軽い版の Android アプリ化 |
| oxlint | MIT | コードの検査 |
| sharp | Apache-2.0 | アイコン画像の生成（開発時のみ。リポジトリには含めていない） |

## フォント（すべて SIL Open Font License 1.1）

| 名前 | 用途 | ライセンス文 |
| --- | --- | --- |
| Zen Kaku Gothic New | 日本語 | `unity/onsite/Assets/Onsite/Fonts/OFL-zenkakugothicnew.txt` |
| Barlow Condensed | 細い英字の見出し | `unity/onsite/Assets/Onsite/Fonts/OFL-barlowcondensed.txt` |
| Share Tech Mono | 数字・座標・小さな英字 | `unity/onsite/Assets/Onsite/Fonts/OFL-sharetechmono.txt` |
| Noto Sans JP / Poppins / DotGothic16 | 軽い版（Google Fonts から読み込み） | — |

## 3D モデル・画像・アイコン

- トロフィー像のアバター（4 体型 × 3 ポーズ）は、Blender（4.5）を Python スクリプト（`unity/blender/build_avatar.py`）で動かして作った自作のモデル。外部のモデル素材は使っていない。
- 地図の画面効果、レーザーの線、六角形の印、画面のアイコン（地図・フィード・＋・ルート・お知らせ・自分）は、すべてこのリポジトリのコード（シェーダーと UI Toolkit の描画）で描いたもの。外部の画像素材は使っていない。
- アプリのアイコンとロゴもこのリポジトリのコードで作成。
- デモ用のサンプル投稿（浅草）の文章は Claude Code が下書きしたもの（投稿者名「onsite サンプル」）。写真は使っていない。

## 参考にしたもの

- 地図の見せ方は、ゲーム『DEATH STRANDING』の地図画面の雰囲気を参考にした（ロゴ・アイコン・画像・名称は使っていない）。
- アバターは、映画賞のトロフィー像の雰囲気を参考にした（実在の像の形やロゴは使っていない）。

## 開発の補助（AI）

- Claude Code（Anthropic）：設計、作業の分担と指示、シェーダー、地図の処理、サーバー、ビルドと配信、動作確認、説明書の下書き
- Codex（OpenAI）：Firebase の REST 通信層、画面（UI Toolkit）、アバター生成スクリプト
- 詳しくは [product.md](product.md) の「AI をどこで、どう、なぜ使ったか」
