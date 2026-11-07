# Task: HUD and screens for the Unity app "onsite" (UI Toolkit, C#)

You are building the whole user-facing UI and the app flow of a Unity 6000.3 (URP) app.
Write ONLY under `unity/onsite/Assets/Onsite/App/` and `unity/onsite/Assets/Onsite/UI/`.
Do not edit other folders (you may read anything). Do not run Unity. No API keys in code.

## The product (read these first)
- `README.md`, `docs/product.md` — what onsite is: posts left at real places; the map shows only a teaser; you can open the body/photo only when you are physically within the post's radius (server checks it). Guestbook comments & likes only for people who opened it. Events = ordered routes of posts. Own trail, footprints. Level/XP from opening/placing.
- The previous web version for behaviour reference: `web/src/App.tsx`, `web/src/components/*.tsx`.

## What already exists (use, don't rewrite)
- `Assets/Onsite/Net/*` — `OnsiteApi` singleton (coroutines for every backend call, polling, events `DropsChanged`, `MyLogChanged`, `NoticesChanged`, `Error`), models in `Models.cs`, `DeviceBridge` (GameObject `OnsiteBridge`; events `OnLocation(lat,lng,acc)`, `OnPhotoReady(byte[])`, `LocationError`; `StartLocation()`, `TakePhoto()`).
- `Assets/Onsite/Runtime/Map/*`:
  - `MapWorld` (`SetOrigin(lat,lng)`, `ToUnity`, `OnGround(lat,lng,lift)`, `FromUnity(world)`, `HasOrigin`, `OriginChanged`, async `GroundHeight`, `Prefetch`).
  - `MarkerField` (`SetDrops(drops)`, `UpdateStates(uid, openedIds, lat, lng, acc)`, `SelectedId`, `ScreenAnchors` → (drop, worldTop, DropState), `Relayout()`, static colors `Cyan`/`Amber`/`Dim`/`White`, `StateOf(...)`).
  - `PathLayer` (`SetTrail(points)`, `SetRoute(stops)`, `Relayout()`).
  - `AvatarRig` (`Apply(AvatarLook)`, `scale`, `spin`), `AvatarLook` (`body` Gentleman/Lady/Gown/Goblin, `pose` Stand/Wave/Pose, `finish` Gold/Silver/RoseGold/Crystal, `Load()`, `Save()`).
  - `OrbitCamera` (`target`, `distance`, `pitch`, `yaw`, `IsOverUi` callback, `SnapToTarget()`).
  - `GeoMath` (`Distance`, `Within`, `Move`, `Format`).
- Scene is built by `Assets/Onsite/Editor/OnsiteSceneBuilder.cs`: it adds a `UIDocument` GameObject named `HUD` and adds the component type `Onsite.App.OnsiteApp` to it (so create exactly that class name/namespace). Fonts are in `Assets/Onsite/Fonts/` (Zen Kaku Gothic New Regular/Medium/Bold for Japanese, Barlow Condensed Light/Regular/Medium/SemiBoldItalic for English headings, Share Tech Mono for numbers).

## Visual direction (this is the most important part — it must look premium, not like a web template)
A dark, cinematic **field-survey HUD** over a holographic 3D terrain map. Original design; do not copy any game's UI, logos or icons. Language:
- Palette: background panels `rgba(6,12,15,0.86)`, hairlines `rgba(127,227,255,0.35)`, primary cyan `#7FE3FF`, accent amber `#FFB24A`, text `#E6F4F7`, muted `#7FA3AB`, danger `#FF6B5A`.
- Panels: square corners, 1px hairline borders, **corner tick marks** (short L-shaped brackets at the 4 corners), thin header rule, small uppercase English section labels in Barlow Condensed with wide letter-spacing (e.g. `FIELD SIGNALS`, `OPEN ORDER`, `LOG`) next to Japanese titles. Big titles may use Barlow Condensed SemiBoldItalic.
- Numbers (distance, counts, coordinates, XP) always in Share Tech Mono.
- Lists look like data tables: column header row in tiny uppercase (`NO / NAME / DIST / RADIUS / ♥`), rows separated by hairlines, the selected row filled with cyan at 85% with dark text; a leading triangle glyph ▽ per row.
- Progress bars are segmented (many small cells), cyan or amber.
- World-anchored tags: for each drop in `MarkerField.ScreenAnchors`, draw a small hexagon icon (`?` locked cyan, `!` in-range amber pulsing, `✓` opened dim, `★` mine white) plus, for the nearest 6 and the selected one, a tag with the teaser (truncated) and a distance box like `1152 m` with a pointer notch. Hide tags behind the camera. Tapping a tag selects the drop.
- Subtle motion: panels slide/fade in 180–240 ms, selection flashes, the in-range icon pulses. Use UI Toolkit transitions (USS `transition`) and `schedule.Execute` animations; keep 60 fps.
- Draw custom shapes (hexagons, corner ticks, segmented bars, scan lines, crosshair) with `generateVisualContent` + `Painter2D`.
- Use USS files for styling (one theme file `UI/onsite.uss`), build the tree in C# (no UXML needed). Assign fonts with `-unity-font-definition` / `-unity-font` via `Font` assets loaded from `Assets/Onsite/Fonts` (load with `AssetDatabase` is not allowed at runtime — reference them from a ScriptableObject `UI/HudTheme.asset`-style class that the scene builder can fill, OR place copies under a `Resources` folder you create inside `Assets/Onsite/UI/Resources/` and load with `Resources.Load<Font>`; choose the Resources approach, and copy the TTFs there yourself is NOT possible without Unity, so instead write USS `url("project://database/Assets/Onsite/Fonts/xxx.ttf")` references — UI Toolkit resolves these at import time).

## Screens & flow (`Onsite.App.OnsiteApp`, a MonoBehaviour on the `HUD` UIDocument)
1. **Boot**: `OnsiteApi.Instance.EnsureSignedIn()`, start location via `DeviceBridge`, first fix → `MapWorld.SetOrigin`, place `AvatarRig` (`FindFirstObjectByType`) at `OnGround(lat,lng)`, `OrbitCamera.SnapToTarget()`. Re-set origin when the user moves > 3 km from it.
2. **Onboarding** (first launch, `PlayerPrefs` `onsite.name` empty): full-screen panel over the map with the 3D avatar centered (move `OrbitCamera` close: distance ~40, pitch ~12, and `AvatarRig.spin = true`), title `ONSITE` (Barlow Condensed Light, very wide tracking) + 「近づくと、ひらく。」, three short lines explaining the concept, then pickers: BODY (gentleman / lady / gown / goblin with Japanese labels 紳士・レディ・ドレス・ゴブリン), POSE (立つ・手をふる・ポーズ), FINISH (ゴールド・シルバー・ローズゴールド・クリスタル), nickname field, button 「この像で出発する」. Changes apply live to the avatar.
3. **Map HUD** (main):
   - Top-left block: `ONSITE` wordmark small, below it area label `FIELD / 35.70881 N 139.72221 E` (mono) and GPS accuracy, and a `DEMO` chip when in demo mode.
   - Top-right: level block `LV.3` + segmented XP bar (XP formula as `web/src/types.ts` `levelOf`), today’s trail distance.
   - Left: a vertical rail of square icon buttons (draw simple line icons with Painter2D): MAP, FEED, POST(+), NOTICE (badge with unread count), ME.
   - Right side: compact list panel `FIELD SIGNALS 近くの予告` (nearest 8 by distance) as a data table; tapping a row selects and flies the camera there (`OrbitCamera.target` can stay the avatar; instead temporarily set yaw toward it — or keep simple: select + show detail).
   - Bottom: status bar with segmented gauges (opened today, placed today), coordinates, and the primary action area.
   - **Selected drop panel** (slides up from bottom): author, time ago, teaser (large), chips `👣 n ♥ n 💬 n` replaced by small drawn icons + mono numbers, radius, distance; segmented approach gauge; primary button 「ひらく」 when in range (or own/opened → 「読む」), otherwise disabled 「あと 230 m」.
   - Demo mode: tapping empty map moves the player there (raycast to y≈ground via `MapWorld.FromUnity` of the hit point on a horizontal plane at the avatar height). Toggle from the ME screen and from `?debug=1`-like dev panel (long press on the wordmark).
4. **Open**: call `OpenDrop` with current lat/lng/acc and nickname. On success, if `firstTime`, play an unlock sequence (full-screen: scan line sweeps, hexagon assembles, big text `UNLOCKED` + 「ひらいた」, amber particles drawn with Painter2D, +30 EXP) ~1.6 s; then show the **Letter** panel: teaser, photo (decode data URL to Texture2D), body, like toggle (`ToggleLike`), guestbook list (`ListComments`) + input (`AddComment`), delete (own posts, with in-app confirm), close.
5. **Post**: enter placement mode — a crosshair at screen center on the map; confirm uses the ground point under the screen center. Form: 予告 (≤60), 本文 (≤600), radius segmented 100m/300m/500m/1km, photo button → `DeviceBridge.TakePhoto()` → preview → upload with `UploadJpeg` → `CreateDrop`. Warn if within 30 m of the player (privacy).
6. **Feed**: stacked full-width cards of nearby teasers (sort near/new), each with a hex glyph, author, time, distance; tap → select on map.
7. **Events**: list (status live/upcoming/ended, joined, progress), detail (stops table with ✓, total route length, join/leave, 「ルートを表示」 → `PathLayer.SetRoute`), create (pick 2–20 drops in order, title, description). Completion of a joined event → `COMPLETE` sequence (same style as unlock).
8. **Notices**: table of notifications (open/comment/like) with unread highlight; tap → select the drop.
9. **Me**: avatar turntable (same as onboarding pickers to change look), stats grid (opened/placed/opened-by-others/likes) in mono, today’s log list, settings: demo mode, share footprints (send trail cells via `AddFootprints` every 30 s when on; default off), reset.
10. **Trail**: record own position every 15 m into `PlayerPrefs` per day (JSON), feed `PathLayer.SetTrail` with today’s points.
11. Errors from the server (Japanese) appear as a toast in the HUD style.

## Code quality
- Split into focused files: `OnsiteApp.cs` (flow/state), `Hud/*.cs` (one class per panel/screen), `Ui/*.cs` (reusable controls: `HudPanel`, `CornerTicks`, `HexIcon`, `SegmentBar`, `DataTable`, `IconButton`, `Toast`, `Crosshair`), `UI/onsite.uss`.
- No `async void` except Unity event handlers that must be; prefer coroutines (OnsiteApi is coroutine-based).
- Must compile on Unity 6000.3 without the editor open: use only UnityEngine.UIElements runtime APIs. Avoid `UnityEditor` in runtime code.
- Comments in Japanese, short, explaining why. Mobile portrait first (1080×1920 reference), but must also work on a desktop browser window.
- At the end, print a summary of the files and anything you could not finish.
