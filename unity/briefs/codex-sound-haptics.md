# Task: sound design + haptics for "onsite" (Unity 6, WebGL + Android)

The app has NO sound today and only one vibration. Add a cinematic, minimal "laser HUD" sound layer and tactile feedback.
Read first: `unity/onsite/Assets/Onsite/App/OnsiteApp.cs`, `OnsiteApp.Actions.cs`, `App/Hud/*.cs`, `App/Ui/*.cs`, `Net/DeviceBridge.cs`, `Plugins/WebGL/OnsiteBridge.jslib`, `docs/product.md` (what the app is).

## Rules
- Write only under `unity/onsite/Assets/Onsite/App/`, `unity/onsite/Assets/Onsite/Net/DeviceBridge.cs`, `unity/onsite/Assets/Plugins/WebGL/OnsiteBridge.jslib`, `unity/onsite/Assets/Onsite/UI/` (both `onsite.uss` and `Resources/onsite.uss` must stay identical).
- Do NOT run Unity. Must compile on Unity 6000.3. Comments in Japanese, short.
- **No external audio files.** Synthesize every sound in C# (`AudioClip.Create` with generated PCM, 44.1 kHz mono, cached once at startup). Keep total generation fast (< 150 ms).
- Do not change backend calls or flow logic.

## Sound palette (subtle, premium, sci‑fi survey terminal; never cartoonish; short)
| id | when | character |
|---|---|---|
| tap | any button / rail icon press | very short soft click + tiny high sine blip (20–40 ms) |
| select | a drop is selected (map, list, feed) | two quick rising sine pings with slight detune |
| open_panel / close_panel | screens and the selected-drop sheet appear/disappear | soft filtered noise "whoosh" up / down (120–180 ms) |
| arrival | entering a drop's radius ("SIGNAL IN RANGE") | sonar ping: sine ~1.2 kHz with exponential decay + faint echo, then a low pulse |
| verify | while the server checks an open (button "VERIFYING") | quiet ticking scan (loop until done) |
| unlock | unlock sequence (`SequenceOverlay.Play(false…)`) | rising laser sweep (FM sine 300→2 kHz) + bright major chord bloom (3 sines, soft attack, ~1.2 s tail) |
| complete | route complete sequence | longer chord progression, 2 s, amber feel (lower, warmer) |
| like | like toggled on | soft high "glint" |
| send | guestbook sent / post placed | short upward swoosh + click |
| error | toast for errors / too far | low muted double buzz (not harsh) |
| ambient | while on the map | very quiet low drone (filtered noise + 55 Hz sine, slow LFO), loops seamlessly; volume ~0.06; fades out on content screens to ~0.02 |

Implementation:
- `App/Sound.cs`: static or MonoBehaviour service with `Play(string id)`, `StartLoop/StopLoop`, master volume, `Muted` (PlayerPrefs `onsite.sound`, default on). Use a small pool of AudioSources on one GameObject created at runtime.
- WebGL audio only starts after a user gesture — make sure the first tap resumes audio (Unity usually handles this; just don't crash before).
- Wire calls at the right places (IconButton/UiKit buttons tap; SelectDrop; Screen() open/close; NotifyArrivals; OpenRoutine verify/unlock; PlayComplete; Like; Comment/Post success; Toast errors). Avoid sound spam: rate-limit identical ids (e.g. 60 ms) and don't play "tap" when another sound is triggered by the same press.
- Add a toggle in the ME screen settings: 「サウンド」 on/off.

## Haptics
- Extend `DeviceBridge` with `Haptic(HapticKind kind)`: kinds Tap (8 ms), Select (14 ms), Arrival (pattern 30,60,30), Unlock (pattern 20,40,20,40,80), Complete (pattern 40,60,40,60,120), Error (60,40,60).
- WebGL: jslib `Onsite_VibratePattern(ptr,len)` → `navigator.vibrate([...])` in try/catch (works on Android Chrome; iOS ignores — fine). Android player: `Handheld.Vibrate()` for anything ≥ Select (no fine control available without plugins). Respect a setting `onsite.haptics` (default on) with a toggle next to sound.
- Replace the existing `bridge?.Vibrate(120)` in NotifyArrivals with the Arrival haptic.

## Touch feel (visual)
- Every button: on PointerDown scale to 0.97 and brighten; on release spring back (80–120 ms, USS transition on `scale`/`background-color` is fine). Add the class in UiKit/IconButton so all buttons get it.
- Rail icon press: brief cyan glow behind the icon.

## Deliverable
Edit in place, then write a short summary to stdout (files changed, ids wired where, anything not done).
