# ここだけ — 近づくと、ひらく。

誰かがその場所に残した投稿を、**実際に近づいたときだけ読める**散歩SNSです。

地図には投稿の「予告」だけが見えます。本文と写真は、投稿者が決めた範囲（100m・300m・500m）に入った人だけが読めます。写真を見た時点で終わってしまう普通のSNSと違い、誰かの発見が「自分で外に出て確かめる」きっかけになります。

- カテゴリ：観光
- デプロイ先：https://kokodake-t009.web.app （審査・デモ用は https://kokodake-t009.web.app/?demo=1 ）
- 説明書：[docs/product.md](docs/product.md)
- 使用した素材・ライブラリ：[docs/credits.md](docs/credits.md)

## 審査・デモで試すとき

現地に行けない人のために **デモモード** があります。右上のボタンを「デモ：タップで移動」に切り替えると、地図をタップした場所を現在地として扱います。URL に `?demo=1` を付けて開いても同じです。

## しくみ

| 部分 | 使っているもの |
| --- | --- |
| 画面 | React + Vite、Google Maps JavaScript API（`@vis.gl/react-google-maps`） |
| ログイン | Firebase Authentication（匿名ログイン） |
| データ | Cloud Firestore |
| 写真 | Cloud Storage for Firebase |
| 距離の判定 | Cloud Functions for Firebase（`asia-northeast1`） |
| 公開 | Firebase Hosting |

本文と写真は、ブラウザから直接は読めないようにしています（`firestore.rules`・`storage.rules`）。読むときは Cloud Functions の `openDrop` が、送られてきた現在地と投稿の場所の距離を計算し、範囲内のときだけ返します。GPS の誤差は最大 30m まで考慮します。

ただし現在地は端末が申告する値なので、GPS の偽装までは防げません。「現地にいることの証明」ではなく「近づくと読める体験」として設計しています。

投稿者のプライバシーのため、投稿の場所は現在地ではなく地図上で自分で選びます。今いる場所にそのまま置こうとすると注意を出します。

## 開発

必要なもの：Node.js 22、Java 21（エミュレーター用）

```bash
npm run setup
```

ローカルのエミュレーターで動かす場合（クラウドのリソースは使いません）：

```bash
cp web/.env.example web/.env.local   # VITE_USE_EMULATORS=true と VITE_FIREBASE_PROJECT_ID=demo-kokodake にする
npx firebase emulators:start --only auth,firestore,functions,storage --project demo-kokodake
npm run dev
```

Google Maps の API キーがないときは、地図の代わりに簡易レーダー表示になります。

距離計算のテスト：

```bash
npm test
```

## デプロイ

1. Firebase コンソールで、Google Cloud プロジェクト `kokodake-t009` を Firebase に追加し、Blaze プランにする
2. Authentication で「匿名」を有効にする
3. Firestore と Storage を作る（場所は `asia-northeast1`）
4. プロジェクトの設定 → マイアプリ → ウェブアプリを追加し、表示された値を `web/.env.local` に入れる
5. Google Cloud コンソールで Maps JavaScript API を有効にし、API キーを作って `web/.env.local` に入れる。キーには HTTP リファラー制限（`https://<プロジェクト>.web.app/*` と `http://localhost:5173/*`）をかける
6. デプロイする

```bash
npx firebase login
```

```bash
npm run deploy
```

`.env.local` は `.gitignore` に入っているのでコミットされません。API キーやクレジットコードはこのリポジトリに書かないでください。
