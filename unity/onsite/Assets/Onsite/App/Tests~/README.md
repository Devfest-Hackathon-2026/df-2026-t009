# UI verification without Unity

Run these from the repository root in PowerShell:

```powershell
& unity/onsite/Assets/Onsite/App/Tests~/TestHelpers.ps1
& unity/onsite/Assets/Onsite/App/Tests~/Compile.ps1
& unity/onsite/Assets/Onsite/App/Tests~/TestStyles.ps1
& unity/onsite/Assets/Onsite/App/Tests~/TestSound.ps1
node unity/onsite/Assets/Onsite/App/Tests~/HapticsBridgeTests.cjs
```

The PowerShell checks use the compiler/runtime bundled with Unity 6000.3.18f1 and never start the Unity editor. Pass `-UnityEditor` to select another installation. The JavaScript check requires Node on PATH.

`Compile.ps1` compiles the app, UI controls, existing Net code and existing Map code against the installed Unity runtime assemblies, including the real UI Toolkit types. It replaces only `MapWorld` with its observed public contract to keep the optional Cesium dependency outside this standalone check. EventSystem and input module types use the real `UnityEngine.UI.dll` from imported project assemblies or the installed Unity template cache. Pass `-Platform UNITY_WEBGL` or `-Platform UNITY_ANDROID` to compile those conditional branches. This verifies C# and UI Toolkit API compatibility, not a full Unity project import/build, USS asset import or a rendered screen.

`TestHelpers.ps1` compiles and runs focused helper contracts using in-memory PlayerPrefs and a JSON adapter. It checks trail sampling/persistence/day changes/pruning/reset, XP thresholds, distance formatting and event route calculations. Unity native storage and JSON behavior still require an eventual Unity run.

Generated binaries and response files remain inside this ignored-by-Unity `Tests~` folder and are ignored by its local `.gitignore`.

`TestStyles.ps1` uses the installed Unity ExCSS tokenizer to check stylesheet syntax and verifies the referenced font paths. It intentionally accepts Unity-specific declaration names; semantic USS validation, asset URL resolution and rendered layout still require Unity import.

`HapticsBridgeTests.cjs` runs under Node and checks the six vibration patterns, WebGL memory bounds, cancellation, and unsupported browser APIs.

`TestSound.ps1` uses the real PCM generator without launching Unity. It checks all twelve clips, finite samples and mixing headroom, deterministic output, click-free ends, the ambient loop seam, and cold generation under 150 ms on the current host. Native AudioClip upload time and device playback remain outside that measurement.
