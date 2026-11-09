# Task: autopilot demo tour (`?tour=1`) with on-screen captions — for recording the submission video

We must record a ~90 s demo video with no human operating the app. Add an autopilot that plays the core story by calling the app's own methods, with cinematic captions drawn in the HUD style. Also remove leftover debug logging.

Read first: `unity/onsite/Assets/Onsite/App/OnsiteApp.cs`, `OnsiteApp.Actions.cs`, `App/Hud/*.cs` (MapHud, EventsScreen, LetterScreen, OnboardingScreen), `App/Ui/*`, `App/Sound.cs`, `Runtime/Map/OrbitCamera.cs`, `Runtime/Map/AvatarRig.cs`.

## Rules
- Write only under `unity/onsite/Assets/Onsite/App/`, `unity/onsite/Assets/Onsite/UI/` (keep `UI/onsite.uss` and `UI/Resources/onsite.uss` identical), and `unity/onsite/Assets/Onsite/Runtime/Map/` (only if needed for camera tweening helpers).
- Do NOT run Unity. Must compile on Unity 6000.3. Japanese comments, short. Don't change backend behaviour.
- Normal use (no `tour=1`) must behave exactly as now.

## Tour (`App/DemoTour.cs`, started from OnsiteApp.Start when `QueryFlag("tour")`; it implies demo mode)
Use real data that already exists around Asakusa (drops by "onsite サンプル": 雷門の提灯、真下から見上げて (35.71106,139.79631 r100), 仲見世で、揚げたてを片手に, 宝蔵門の裏側を見たことある？, 本堂に入ったら、天井を, 隅田川テラスで、塔がふたつに …; event 「浅草 はじめてめぐり」). Find drops/events by teaser/title prefix; fall back gracefully (skip a step) if missing.
Timeline (approximate; wait for real async results where needed, e.g. Api.Refresh, open result):
1. **0–8 s Onboarding**: if no nickname, show onboarding; slowly cycle avatar body/finish 3 times (via `SetLook`), then set nickname 「旅人」 and depart (same code path as the button). If a nickname exists, still show the avatar portrait for ~4 s (e.g. Me-style portrait camera) then go to the map.
   Caption: 「行かないと、読めない SNS。」 / small: `ONSITE — 近づくと、ひらく。`
2. **8–18 s Map overview**: start at demo point (35.71065,139.79745). Camera: distance ~650, pitch ~60, slow yaw drift (+8°/s). Caption: 「地図に見えるのは、予告だけ。」 / 「中身は、その場所に行った人にしか読めない。」
3. **18–30 s Walk**: camera eases to distance ~260, pitch ~55. Move the player toward 雷門 in small steps (every 0.25 s, ~4 m per step) via the same path as a demo tap (`SetPosition`), so the trail laser draws. Stop ~40 m from the drop. The existing arrival logic fires the toast + auto-select. Caption: 「浅草を歩くと――」 then on arrival 「範囲に入った瞬間、知らせが届く。」
   If 雷門 is already opened by this browser's user, use the first not-yet-opened sample drop instead (walk to it). If all are opened, walk to 雷門 anyway and press READ.
4. **30–46 s Open**: wait 1.5 s, call `OpenSelected()`. Unlock sequence + letter. Caption during sequence: 「距離はサーバーが確かめる。だから、先には読めない。」 Let the letter be readable ~5 s (scroll at top).
5. **46–60 s React**: toggle like (only if not already liked). Scroll the letter down to the guestbook, put text 「龍、いました！首が痛い。」 into the input (set `.value` directly), but **do not send** (avoid writing to production on every recording) — show it for 3 s then close. Caption: 「ひらいた人だけが、寄せ書きできる。」
6. **60–80 s Route**: open the Events screen and the detail of 「浅草 はじめてめぐり」 for ~4 s (caption 「投稿をつなげて、巡りルートに。期間限定の祭りやツアーにも。」), then `ShowRoute(ev)` (do NOT join). On the map: camera pulls out to distance ~900, pitch ~62, yaw drifting so the route lasers and stops are visible. Caption: 「歩いた道は、光の線で残る。」
7. **80–90 s End card**: fade in a full-screen dark overlay with big tracked `ONSITE` + 「近づくと、ひらく。」 + small mono `kokodake-t009.web.app`. Hold.
- While the tour runs: block user map taps/drags (they would break it), hide the debug panel. Audio may stay silent until a real user gesture — acceptable.
- `?tour=1&start=N` optional: skip to step N (for retakes).

## Captions UI (`App/Ui/Caption.cs` + USS)
- Bottom-center (above the status strip / phone tab bar), max width ~80%. Large line (Zen Kaku Gothic New Medium, ~34px, white with glow) + optional small mono line. A thin laser band draws in under it (reuse `LaserBand`). Fade/slide in 300 ms, out 250 ms. Must not intercept input.

## Cleanup of debug logging (keep behaviour)
- Remove the temporary `Debug.Log` lines that start with `[onsite] avatar`, `[onsite] height`, `[onsite] origin`, `[onsite] open result`, `[onsite] sequence done`, `[onsite] showing letter`, `[onsite] click`, `[onsite] pick`, `[onsite] ui pointerdown` (in App/ and Runtime/Map/). Keep warnings/errors.

At the end print a summary (files, how to start, anything not done).
