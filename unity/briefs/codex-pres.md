# Task: presentation cards for the tour (`?tour=1&pres=1`) — turns the demo into a ~3 min presentation video

The online venue needs a presentation video (MP4, ≤5 min, must include the demo). We record the app's own autopilot tour (`App/DemoTour.cs`, captions in `App/Ui/Caption.cs`). Add full-screen **presentation cards** before and after the existing tour when `pres=1` is in the URL. Without `pres=1` nothing changes.

Rules: write only under `unity/onsite/Assets/Onsite/App/` and `unity/onsite/Assets/Onsite/UI/` (keep `UI/onsite.uss` == `UI/Resources/onsite.uss`). Do not run Unity. Must compile on Unity 6000.3. Japanese comments. Reuse the end-card look (dark overlay, thin tracked Barlow Condensed Light heading, Zen Kaku Gothic body, cyan laser band drawing in). Map keeps rendering behind at low opacity (overlay ~0.88). Each card: small mono kicker (e.g. `01 / PROBLEM`), big heading, 2–4 short body lines that appear one by one (300 ms stagger), hold, then cross-fade (400 ms) to the next. Use only glyphs available in Zen Kaku Gothic New / Barlow Condensed / Share Tech Mono (avoid em-dash `—` and `――`; use `/` or `、`).

## Intro (before step 1), ~60 s total
1. (6 s) kicker `DEVFEST GDGOC JAPAN HACKATHON 2026 / TEAM t009`, heading `ONSITE`, body: 「近づくと、ひらく。」「行かないと読めない、おでかけ SNS」
2. (12 s) `01 / PROBLEM` 「写真を見たら、満足してしまう。」 body: 「SNS で旅先の景色を見ても、それで終わってしまう。」「観光情報は有名な場所に偏り、地元の小さな発見は届かない。」
3. (12 s) `02 / IDEA` 「中身は、その場所に行った人だけ。」 body: 「地図に見えるのは “予告” だけ。」 → use 「地図に見えるのは「予告」だけ。」 「投稿者が決めた範囲（100m〜1km）に入ると、本文と写真がひらく。」「ひらいた人だけが、寄せ書きできる。」
4. (10 s) `03 / FOR TRAVEL` 「巡りルートで、観光の回り方を共有。」 body: 「投稿をつないでルートに。開催期間を付ければ、祭りやツアーの期間限定の巡りにも。」
5. (5 s) `04 / DEMO` 「浅草で、試してみる。」 → then the existing tour runs (its own captions).

## Outro (replace the existing end card when pres=1), ~50 s
6. (14 s) `05 / HOW IT WORKS` 「距離の判定は、サーバーで。」 body: 「本文・写真・寄せ書きは Firestore のルールで遮断し、Cloud Functions が現在地との距離を確かめた時だけ返す。」「だから、通信を覗いても先には読めない。」
7. (14 s) `06 / GOOGLE TECHNOLOGIES` 「使った Google の技術」 body lines: 「Firebase Hosting / Cloud Functions / Cloud Firestore / Cloud Storage / Authentication」「Google Maps Platform：Photorealistic 3D Tiles（Map Tiles API）」「Unity 6 ＋ Cesium で、立体地図を地形模型のように描画」
8. (10 s) `07 / NEXT` 「これから」 body: 「存在ごと隠すシークレット投稿」「主催者向けの特典と集計」「通報と非表示」
9. (10 s) the existing end card: `ONSITE` 「近づくと、ひらく。」 `kokodake-t009.web.app`, hold.

Implementation notes: put the cards in a new `App/Ui/PresentationCard.cs` (+ USS), driven from `DemoTour` (coroutine before/after). During cards, hide the normal caption and block input like the tour already does. Print a short summary at the end.
