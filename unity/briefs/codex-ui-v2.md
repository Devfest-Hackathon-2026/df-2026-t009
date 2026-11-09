# Task: restyle the whole UI of "onsite" — cinematic, stylish, "laser" HUD

The app works (Unity 6000.3, URP, UI Toolkit, C#). The user's verdict on the current UI:
"the map and the trophy avatar are good, but the UI and how things appear still look cheap.
I want Kojima Productions–like style. Stylish. A laser feel. Plus the clean, photo-first feel of Instagram / setlog."

Your job: make the UI look premium. Rewrite styling and the visual parts of the HUD/screens; keep behaviour.

## Scope
- Write ONLY under `unity/onsite/Assets/Onsite/App/` and `unity/onsite/Assets/Onsite/UI/`.
- Do NOT edit `Runtime/`, `Net/`, `Editor/`, `Shaders/` (another engineer is changing the 3D lines/shaders now). You may read them.
- Do NOT run Unity (the project is open in a build). It must compile on Unity 6000.3 with runtime UI Toolkit APIs only.
- Keep the public API that `OnsiteApp` and other files use; keep all features (onboarding, map HUD, select/open/letter/guestbook/like/delete, post placement + form + photo, feed, events, notices, me/settings, demo/debug, toasts, unlock/complete sequences).
- Comments in Japanese, short.

## References (look at the images!)
`unity/briefs/ref-ds/` — 1.png … 12.webp are screenshots of the reference look the user wants (game map UI). `current-app.jpg` is our app now.
Do not copy any game's logos, icons, names or exact layouts; take the design LANGUAGE.

What makes the reference feel premium (apply all of this):
1. **Almost no boxes.** Content floats on the map. Instead of filled dark rectangles with borders, use:
   - text with a soft dark glow/shadow for legibility (`text-shadow`),
   - horizontal **light bands**: a thin bright line (1–2px, cyan-white) with a soft gradient glow fading out to one side, under a title (e.g. the area title bar fading to the right),
   - very thin hairlines (1px, `rgba(160,230,255,0.35)`) and **corner brackets** only at a few anchor points,
   - where a backing is really needed (lists, forms), a vertical/horizontal gradient from `rgba(4,10,16,0.82)` to transparent, never a hard-edged opaque rectangle. Use `generateVisualContent` + `Painter2D` or a generated gradient `Texture2D` (cache it) for gradients.
2. **Typography is the design.** Big, thin, widely tracked Latin headings (Barlow Condensed Light, letter-spacing 0.2–0.35em) like `MAP / INFORMATION`, `FIELD`, `ONSITE`; a small mono sub-label (Share Tech Mono, 0.7 size, muted) above/below; Japanese in Zen Kaku Gothic New Medium, smaller, beside the Latin. Numbers big in thin mono (`24850 / 45840` style for XP, `1725 m` for distance). Strong size contrast between headline and labels.
3. **Laser.** Lines are light, not strokes: a bright near-white core with cyan glow. Animate them: lines **draw in** from one end (width 0 → full, 220–320 ms, ease-out), a scan highlight sweeps along a band occasionally, selection makes a quick bright flash then settles. Underlines and brackets extend in when a panel appears. Keep it subtle and fast — no bouncing, no slow fades.
4. **Left vertical icon rail**: thin outline line icons (draw with Painter2D, 1.5px), no square tiles behind them; the active one gets a short vertical cyan bar on its left and a soft glow; small label appears next to the active icon. Unread badge = small amber dot + mono number.
5. **Bottom-right "button hints"** style for primary actions: a small circle glyph + Japanese label (e.g. `◎ ひらく`, `○ 閉じる`), and the main action as a wide light band button with the label in tracked Latin + Japanese (`OPEN  ひらく`), disabled state shows mono remaining distance `あと 230 m` in muted color.
6. **World tags** (labels over posts on the 3D map): white text with glow over a light band that fades out at both ends + small line icon; distance in mono cyan right after. Selected = amber band. The tag draws in (band grows from the icon).
7. **Instagram / setlog feel** for content screens (Feed, Letter, Me): photo-first. Full-bleed photos with rounded 18px corners where there is a photo, generous spacing, author line with small round avatar glyph, time in mono, like/comment counts minimal. Text set like a magazine: teaser large, body comfortable line height. Still dark and with the laser accents (a thin glowing rule, a mono caption) so it is one consistent world.
8. **Unlock / Complete sequences**: make them feel like a cinematic HUD event: horizontal laser lines sweep across, the hexagon draws its edges one by one, big tracked `UNLOCKED` / `ROUTE COMPLETE` with a mono sub-line, quick flash, then everything retracts. ~1.6 s.
9. **Motion system**: one small helper in `Ui/` (e.g. `Anim.cs`) for draw-in, fade/slide (8–16 px), flash, using `schedule.Execute` or USS transitions. Panels enter in 180–240 ms with staggered children (30 ms).

Palette: core `#EAFBFF`, cyan `#7FE3FF`, deep cyan `#2E9BD6`, amber `#FFB24A`, muted `#7FA3AB`, danger `#FF6B5A`, shadow `rgba(2,6,10,0.85)`.

Layout target: mobile portrait first (1080×1920 reference, PanelSettings scale-with-screen, match 0.5), must also be usable in a desktop browser window (there are `.narrow` / `.compact` classes already). Keep the map visible: HUD should occupy edges, centre stays clear.

## Deliverable
Edit the files in place (`App/Hud/*.cs`, `App/Ui/*.cs`, `UI/onsite.uss`, small changes in `App/OnsiteApp*.cs` only if needed for the new visuals). Prefer rewriting `onsite.uss` cleanly over appending overrides (the current file has duplicated/overridden rules). At the end, write a short summary of what changed and anything not finished.
