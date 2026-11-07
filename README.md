# onsite — 近づくと、ひらく。

誰かがその場所に残した投稿を、**実際に近づいたときだけ読める**おでかけSNSです。街歩きでも旅先でも使えます。

地図には投稿の「予告」だけが見えます。本文と写真は、投稿者が決めた範囲（100m・300m・500m・1km）に入った人だけがひらけます。写真を見た時点で終わってしまう普通のSNSと違い、誰かの発見が「自分で出かけて確かめる」きっかけになります。

- カテゴリ：観光
- 公開URL：https://kokodake-t009.web.app （審査・デモ用は https://kokodake-t009.web.app/?demo=1 ）
- Android アプリ：`web/android`（Capacitor）。ビルド方法は下の「Android アプリ」
- 説明書：[docs/product.md](docs/product.md)
- 使用した素材・ライブラリ：[docs/credits.md](docs/credits.md)

## 見た目

- **HOLO**（初期設定）：暗い測量図のような地図に、シアンの細い線と六角形の印。現在地には、自分で選んだクリスタルの人形（ポーズと色を選べる）が立つ
- **MILK**：くすみカラーの平面のイラスト地図

プロフィールの「表示」で切り替えられます。地図の配色は [docs/map-style-holo.json](docs/map-style-holo.json)（ダークモード）と [docs/map-style.json](docs/map-style.json)（ライトモード）を、同じマップ ID に設定しています。

## デバッグパネル

URL に `?debug=1` を付けるか、左上のロゴを 5 回続けてタップすると開きます。ワープ、10m・50m ずつの移動、選んだ投稿まで歩く（8 倍速）、テスト投稿の作成と削除、演出の再生、テーマと立体表示の切り替え、状態の確認、記録のリセットができます。距離の判定はサーバーで行うので、デバッグパネルから遠くの投稿をひらくことはできません。

## 審査・デモで試すとき

現地に行けない人のために **デモモード** があります。右上のボタンを「デモ：タップで移動」に切り替えると、地図をタップした場所を現在地として扱います。URL に `?demo=1` を付けて開いても同じです。

1. はじめにニックネームを入れる
2. 地図の「？」の近くをタップして移動し、下の「ひらける投稿があります」→「ひらく」
3. ひらいた投稿には、寄せ書き（コメント）といいねができる
4. 「＋」で投稿を置く（写真はその場で撮影）
5. フィード →「イベント」で、投稿をつないだ巡りルートを作る・参加する

## できること

| 機能 | 内容 |
| --- | --- |
| 近づくとひらく投稿 | 予告は誰でも見える。本文と写真は範囲に入った人だけ。はじめてひらくとドット絵の演出 |
| 寄せ書きといいね | その場所でひらいた人と投稿者だけが書けて、読める |
| フィード | まわりの予告が、近い順・新着順でカードのように重なって流れる |
| イベント（巡りルート） | 投稿を 2〜20 か所つないでルートにする。参加すると地図に番号と矢印が出て、全部ひらくと COMPLETE |
| 足あと | 自分の移動が地図に点線で残る。1 日ごとの距離と道のりの線画がログに並ぶ |
| みんなの踏み跡 | 共有をオンにした人の移動を、約 33m 四方のマス目ごとの回数としてだけ集め、よく通られた道が浮かび上がる |
| おでかけログ | 1 日（午前 4 時区切り）ごとに、ひらいた・置いた・歩いたをまとめ、ストーリーのように再生 |
| プロフィールとレベル | ひらいた数・置いた数・ひらかれた数などから経験値とレベル |
| お知らせ | 自分の投稿がひらかれた・寄せ書きされた・いいねされたときに届く |
| アプリとして | PWA（ホーム画面に追加できる）と Android アプリ |

## しくみ

| 部分 | 使っているもの |
| --- | --- |
| 画面 | React + Vite、Google Maps JavaScript API（`@vis.gl/react-google-maps`） |
| ログイン | Firebase Authentication（匿名ログイン） |
| データ | Cloud Firestore |
| 写真 | Cloud Storage for Firebase |
| 距離の判定・書き込み | Cloud Functions for Firebase（`asia-northeast1`） |
| 公開 | Firebase Hosting |
| Android | Capacitor 7 |

本文・写真・寄せ書きは、ブラウザから直接は読めないようにしています（`firestore.rules`・`storage.rules`）。読むときは Cloud Functions の `openDrop` が、送られてきた現在地と投稿の場所の距離を計算し、範囲内のときだけ返します。GPS の誤差は最大 30m まで考慮します。書き込みもすべて Cloud Functions を通します。

ただし現在地は端末が申告する値なので、GPS の偽装までは防げません。「現地にいることの証明」ではなく「近づくと読める体験」として設計しています。

### プライバシー

- 投稿の場所は現在地ではなく地図上で自分で選ぶ。今いる場所にそのまま置こうとすると注意を出す
- 自分の足あとは端末の中（localStorage）にだけ保存する
- みんなの踏み跡は初期設定でオフ。オンにしても、送るのはマス目ごとの回数だけで、誰が通ったかは残らない

## 開発

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

Google Maps の API キーがないときは、地図の代わりに簡易レーダー表示になります。

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
5. Google Cloud コンソールで Maps JavaScript API を有効にし、API キーを作って `web/.env.local` に入れる。キーには HTTP リファラー制限（`https://<プロジェクト>.web.app/*`、`https://<プロジェクト>.firebaseapp.com/*`、`http://localhost:5173/*`）をかける
   - 地図の見た目：Google Maps Platform の「地図のスタイル」で [docs/map-style.json](docs/map-style.json) を JSON として読み込んで保存する。「マップ管理」で JavaScript・ベクター（チルトとローテーションを有効）のマップ ID を作り、ライトモードにそのスタイルを選ぶ。マップ ID を `web/.env.local` の `VITE_GOOGLE_MAPS_MAP_ID` に入れる
6. デプロイする

```bash
npx firebase login
```

```bash
npm run deploy
```

`.env.local` は `.gitignore` に入っているのでコミットされません。API キーやクレジットコードはこのリポジトリに書かないでください。

## Android アプリ

`web/android` は Capacitor で作った Android プロジェクトです。アプリの中の画面は `https://kokodake-t009.web.app` という名前で動くので、Web 版と同じ Maps キーの制限のまま使えます。

```bash
cd web && npm run build && npx cap sync android
```

```bash
cd web/android && ./gradlew assembleDebug
```

できた APK は `web/android/app/build/outputs/apk/debug/app-debug.apk` です。Windows でパスが長すぎてビルドに失敗するときは、`subst` で短いドライブ名に割り当ててから実行してください。
