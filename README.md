# onsite — 近づくと、ひらく。

誰かがその場所に残した投稿を、**実際に近づいたときだけ読める**、おでかけと観光のための SNS です。

地図には投稿の「予告」だけが見えます。本文と写真は、投稿者が決めた範囲（100m・300m・500m・1km）に入った人だけがひらけます。写真を見た時点で終わってしまう普通の SNS と違い、誰かの発見が「自分で出かけて確かめる」きっかけになります。投稿をつないだ「巡りルート」には開催期間を付けられるので、祭りやツアーの期間限定の巡りにも使えます。

- カテゴリ：観光
- **本体は Android アプリ**（Unity で作った立体地図のアプリ。ビルド方法は下の「Unity 版のビルド」）。ブラウザ版は、アプリを入れずに確認するための入口
- 公開 URL：https://kokodake-t009.web.app （Unity 版が開きます）
- 審査・デモ用：https://kokodake-t009.web.app/?demo=1 （浅草から始まり、地図をタップして移動できます）
- 軽い版（立体地図が重い端末向け）：https://kokodake-t009.web.app/lite
- 自動デモ（字幕つきで勝手に進む）：ブラウザ版は https://kokodake-t009.web.app/?tour=1 、アプリは左上のロゴを長押し →「自動デモ」
- 説明書：[docs/product.md](docs/product.md)
- 使用した素材・ライブラリ：[docs/credits.md](docs/credits.md)

## 審査・デモで試すとき

1. https://kokodake-t009.web.app/?demo=1 をひらく（初回は読み込みに少し時間がかかります）
2. 像の体型・ポーズ・仕上げを選び、ニックネームを入れて出発
3. 浅草の地図に、サンプルの予告（六角形の印）が出ます。地図をタップすると、その場所へ移動します
4. 予告の範囲に入ると「SIGNAL IN RANGE」の知らせが出るので「ひらく」→ 開封の演出 → 本文
5. ひらいた投稿には、寄せ書き（コメント）といいねができます
6. 左（スマホでは下）の「ROUTES」で「浅草 はじめてめぐり」に参加すると、ルートがレーザーで地図に出ます。全部ひらくと制覇の演出
7. 「＋」で照準の場所に投稿を置けます

URL の末尾に `&debug=1` を付けるか、左上のロゴを長押しすると開発用の画面が出ます。距離の判定はサーバーで行うので、ここから遠くの投稿をひらくことはできません。

## できること

| 機能 | 内容 |
| --- | --- |
| 近づくとひらく投稿 | 予告は誰でも見える。本文と写真は範囲に入った人だけ。範囲に入ると知らせが出て、はじめてひらくと開封の演出 |
| 寄せ書きといいね | その場所でひらいた人と投稿者だけが書けて、読める |
| フィード | まわりの予告を近い順・新着順に。写真は「現地でひらく」まで隠れる |
| イベント（巡りルート） | 投稿を 2〜20 か所つないでルートにし、開催期間を付けられる。参加すると地図にルートが出て、全部ひらくと制覇の演出 |
| 足あと・踏み跡 | 自分の移動が地図に光の線で残る。みんなの踏み跡はマス目ごとの回数だけを共有（初期設定でオフ） |
| アバター | トロフィー像（紳士・レディ・ドレス・ゴブリン × 3 ポーズ × 4 つの仕上げ） |
| 経験値とレベル、お知らせ | ひらいた・置いた・ひらかれた数から経験値。自分の投稿への反応が届く |
| デモモード | 現地に行けない人が、地図をタップして移動できる |

## しくみ

| 部分 | 使っているもの |
| --- | --- |
| アプリ本体 | Unity 6（C#）、URP、UI Toolkit。WebGL と Android に書き出し（`unity/onsite`） |
| 立体地図 | Cesium for Unity ＋ Google Photorealistic 3D Tiles。自作シェーダーで地形模型のように見せる |
| 通信 | Firebase の REST API を C# から直接（`unity/onsite/Assets/Onsite/Net`） |
| ログイン | Firebase Authentication（匿名） |
| データ・写真 | Cloud Firestore、Cloud Storage for Firebase |
| 距離の判定・書き込み | Cloud Functions for Firebase（`functions/`、`asia-northeast1`） |
| 公開 | Firebase Hosting（`/` → Unity 版 `/field/`、`/lite` → 軽い版） |
| 軽い版 | React + Vite + Google Maps JavaScript API（`web/`）、Android は Capacitor |
| アバターの 3D モデル | Blender を Python スクリプトで動かして生成（`unity/blender/build_avatar.py`） |

本文・写真・寄せ書きは、ブラウザから直接は読めないようにしています（`firestore.rules`・`storage.rules`）。読むときは Cloud Functions の `openDrop` が、送られてきた現在地と投稿の場所の距離を計算し、範囲内のときだけ返します。GPS の誤差は最大 30m まで考慮します。書き込みもすべて Cloud Functions を通します。

ただし現在地は端末が申告する値なので、GPS の偽装までは防げません。「現地にいることの証明」ではなく「近づくと読める体験」として設計しています。

### プライバシー

- 投稿の場所は現在地ではなく地図の照準で選ぶ。今いる場所のすぐそばに置こうとすると注意を出す
- 自分の足あとは端末の中にだけ保存する
- みんなの踏み跡は初期設定でオフ。オンにしても、送るのはマス目ごとの回数だけで、誰が通ったかは残らない

## Unity 版のビルド

必要なもの：Unity 6000.3（WebGL / Android モジュール）。パッケージは `Packages/manifest.json` のとおり（Cesium は `https://unity.pkg.cesium.com` から）。

1. `web/.env.local` に Firebase と Google Maps の値を入れる（下の「デプロイ」）。Map Tiles API も有効にする
2. ビルドすると `web/.env.local` から鍵を読み、Git に入らない設定ファイル（`Assets/Onsite/Resources/*.asset`）を作る

```bash
Unity -batchmode -nographics -projectPath unity/onsite -buildTarget WebGL -executeMethod Onsite.EditorTools.OnsiteSetup.BuildWebGL -quit
```

```bash
Unity -batchmode -nographics -projectPath unity/onsite -buildTarget Android -executeMethod Onsite.EditorTools.OnsiteSetup.BuildAndroid -quit
```

WebGL の出力（`unity/build/webgl`）を `web/dist/field` にコピーしてから Hosting にデプロイします。マルチスレッドを使うため、`firebase.json` で `/field/**` に COOP/COEP ヘッダーを付けています。

## 軽い版・サーバーの開発

必要なもの：Node.js 22、Java 21（エミュレーターと Android のビルド用）

```bash
npm run setup
```

ローカルのエミュレーターで動かす場合（クラウドのリソースは使いません）：

```bash
cp web/.env.example web/.env.local   # VITE_USE_EMULATORS=true と VITE_FIREBASE_PROJECT_ID=demo-kokodake にする
npx firebase emulators:start --only auth,firestore,functions,storage --project demo-kokodake
npm run dev
```

距離計算のテスト：

```bash
npm test
```

本番の Firebase に一時的な投稿・イベントを作って、2 人のユーザーで確かめる（最後に消します）：

```bash
node scripts/e2e.mjs web/.env.local
```

## デプロイ

1. Firebase プロジェクト（Blaze プラン）を用意し、`.firebaserc` の ID を合わせる
2. Authentication で「匿名」を有効にする
3. Firestore（`asia-northeast1`）と Storage を作る
4. プロジェクトの設定 → マイアプリ → ウェブアプリを追加し、表示された値を `web/.env.local` に入れる
5. Google Cloud コンソールで Maps JavaScript API と Map Tiles API を有効にし、API キーを作って `web/.env.local` に入れる。キーには HTTP リファラー制限（`https://<プロジェクト>.web.app/*`、`https://<プロジェクト>.firebaseapp.com/*`、`http://localhost:5173/*`）をかける
6. デプロイする（軽い版のビルドは `web/dist` を作り直すので、Unity 版の `web/dist/field` はそのあとにコピーする）

```bash
npx firebase login
```

```bash
npm run deploy
```

`.env.local` と Unity の鍵の設定ファイルは `.gitignore` に入っているのでコミットされません。API キーやクレジットコードはこのリポジトリに書かないでください。
