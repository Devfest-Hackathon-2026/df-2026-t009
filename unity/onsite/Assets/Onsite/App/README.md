# onsite UI / app flow

このフォルダーと隣の `UI/` だけで、既存の通信・地図コンポーネントに画面を接続します。Unity エディターは起動していません。

## ファイル

- `OnsiteApp.cs`：起動、現在地、画面遷移、原点の移動、デモ、外見、端末設定、UI の入力判定。
- `OnsiteApp.Actions.cs`：開封・写真アップロード・投稿・寄せ書き・いいね・削除・イベント・踏み跡のコルーチン。認証の世代が変わった応答は以前の本文を再表示しません。
- `ApiBootstrap.cs`：シーンの API 設定が未割当のとき、既存の `Resources/FirebaseConfig` を新しい非アクティブな API に設定してから有効化します。鍵はコードに含めません。
- `TrailStore.cs`：午前4時区切りの足あと。15 m 以上で保存し、14日・1日3000点まで保持します。2 km 以上の移動は距離に加算しません。
- `AppFormat.cs`：距離・座標・日時・イベント状態と Web と同じ XP / レベル式。
- `Hud/`：`MapHud`、`WorldTags`、`OnboardingScreen`、`AvatarPicker`、`FeedScreen`、`LetterScreen`、`PostScreen`、`EventsScreen`、`NoticesScreen`、`MeScreen`。
- `Ui/`：`HudPanel`、`CornerTicks`、`HexIcon`、`SegmentBar`、`DataTable`、`IconButton` / `LineIcon` / `MetricChip`、`Toast`、`Crosshair`、`SequenceOverlay`、`UiKit`。
- `../UI/onsite.uss`：共通テーマ、フォント、画面サイズに応じた配置。`../UI/Resources/onsite.uss` は実行時ロード用の import だけを持ちます。
- `Tests~/`：Unity を起動しない C#・ヘルパー・USS の確認。詳しくは `Tests~/README.md`。

## 操作

名前が空の初回は外見とニックネームを決めます。FEED 内からイベントへ移動できます。POST は照準の地面を確認してから入力します。地図のロゴを850 ms押すか `?debug=1` で開発パネルを開き、ME でもデモを切り替えられます。デモの空白地図タップは、ドラッグと区別してアバターの高さの水平面へ投影します。

本文・写真は `OpenDrop` の成功応答を受け取るまで表示しません。いいね・寄せ書き・削除も既存 API を使います。削除と端末リセットにはアプリ内の確認があります。足あと共有は初期設定オフで、オン以降の GPS の点だけを30秒ごとに最大60点送り、サーバーでマス目に変換します。デモの移動は共有しません。リセットしても認証とサーバーの投稿・開封記録は消しません。

## 確認結果と残る確認

- Unity 6000.3.18f1 の実際の UI Toolkit / UnityEngine アセンブリに対する44ソースのコンパイルに成功。
- 足あと・日付境界・保存・リセット・XP 等の23チェックに成功。
- Unity 付属 ExCSS による USS 構文確認と、7つの参照フォントの存在確認に成功。
- Cesium のインポート済み DLL がないため、コンパイル時の `MapWorld` だけは既存の公開契約を写した shim を使います。Unity プロジェクト全体のビルド成功を意味しません。
- 指示どおり Unity を起動していないため、ネイティブ JSON 参照の受け渡し、USS インポート、実際の画面配置・動作・性能、端末の位置情報・撮影、サーバーとの E2E は未検証です。
- 既存 `DeviceBridge` の非 WebGL 撮影は null を返す実装のままです。UI は取得失敗を表示し、本文のみの投稿も可能です。この端末機能は許可範囲外のため変更していません。

API 起動補完は Unity の [Awake](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/monobehaviour/awake)、[ToJson](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/jsonutility/tojson)、[FromJsonOverwrite](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/jsonutility/fromjsonoverwrite) の実行時仕様に従います。実機での参照復元確認は上記の未検証項目に含みます。
