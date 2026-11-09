# onsite のアーキテクチャ

## 全体の構成

```mermaid
flowchart LR
  subgraph APP["① 端末（onsite アプリ）"]
    direction TB
    NET["通信層<br/>Firebase REST クライアント（C#）"]
    UI["画面<br/>UI Toolkit・開封演出"]
    BR["端末連携<br/>GPS・写真・振動・効果音"]
    MAP["立体地図<br/>Cesium for Unity<br/>＋自作シェーダー"]
  end

  subgraph FB["② Firebase"]
    direction TB
    AUTH["Authentication<br/>匿名ログイン・ID トークン"]
    OPEN["Cloud Functions：openDrop<br/>距離を判定し、範囲内なら<br/>本文と写真を返す"]
    WRITE["Cloud Functions：その他<br/>投稿・寄せ書き・いいね<br/>イベント・踏み跡"]
    HOST["Hosting<br/>ブラウザ版・軽い版の配信"]
  end

  subgraph DATA["③ データと地図"]
    direction TB
    PUBLIC["Firestore：だれでも読める<br/>drops（予告と位置）<br/>events・profiles・footprints"]
    LIMITED["Firestore：限られた人だけ<br/>comments（ひらいた人と投稿者）<br/>users/{uid}（本人だけ）"]
    SECRET["サーバーだけが読める<br/>Firestore private（本文）<br/>Storage uploads（写真）"]
    TILES["Map Tiles API<br/>Photorealistic 3D Tiles<br/>地図専用の鍵・1日の上限"]
  end

  NET -->|ログイン| AUTH
  NET ==>|"開封（ID トークン付き）"| OPEN
  NET -->|書き込み| WRITE
  NET -->|読み取り| PUBLIC
  NET -->|読み取り| LIMITED
  MAP -->|3D タイル| TILES
  OPEN ==>|距離を確かめてから| SECRET
  OPEN -->|開封を記録| LIMITED
  WRITE --> PUBLIC
  WRITE --> SECRET

  classDef key fill:#fff4e0,stroke:#e3962c,stroke-width:2px
  classDef secret fill:#fde8e8,stroke:#c74b4b
  class OPEN key
  class SECRET secret
```

## 「ひらく」までの流れ

```mermaid
sequenceDiagram
  autonumber
  actor U as 利用者
  participant A as onsite アプリ
  participant G as 端末の GPS
  participant F as Cloud Functions（openDrop）
  participant D as Cloud Firestore
  participant S as Cloud Storage

  A->>D: 予告と位置を読む（drops：だれでも読める）
  D-->>A: 予告・半径（本文は含まない）
  loop 歩いているあいだ
    G-->>A: 現在地と精度
    A->>A: 距離を計算し、範囲に入ったら知らせ（効果音・振動）
  end
  U->>A: 「ひらく」
  A->>F: dropId・現在地・精度（ID トークン付き）
  F->>D: 投稿の位置と半径を読む
  F->>F: サーバーで距離を判定（GPS の誤差は最大 30m まで考慮）
  alt 範囲の外
    F-->>A: too_far（本文は返さない）
  else 範囲の中
    F->>D: 開封を記録（users/{uid}/opened）・開封数と経験値を更新・投稿者へお知らせ
    F->>D: 本文を読む（drops/{id}/private：サーバーだけ）
    F->>S: 写真を読む（uploads：サーバーだけ）
    F-->>A: 本文と写真
    A->>U: 開封の演出 → 本文
    Note over A,D: 以後、この人は寄せ書き（comments）を読めて、書ける
  end
```

- 本文・写真・寄せ書きは、Firestore と Storage のセキュリティルールでアプリから直接は読めないようにしています。中身を返すのは、距離を確かめた Cloud Functions だけです。
- 書き込みはすべて Cloud Functions を通します（アプリからデータベースへ直接は書けません）。
- 現在地はサーバーに保存しません。開封したときの距離だけを、本人の記録に残します。
