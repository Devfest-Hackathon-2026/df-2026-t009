# UI verification without Unity

Run these from the repository root in PowerShell:

```powershell
& unity/onsite/Assets/Onsite/App/Tests~/TestHelpers.ps1
& unity/onsite/Assets/Onsite/App/Tests~/Compile.ps1
& unity/onsite/Assets/Onsite/App/Tests~/TestStyles.ps1
```

All commands use the compiler/runtime bundled with Unity 6000.3.18f1 and never start the Unity editor. Pass `-UnityEditor` to select another installation.

`Compile.ps1` compiles the app, UI controls, existing Net code and existing Map code against the installed Unity runtime assemblies, including the real UI Toolkit types. It replaces only `MapWorld` with its observed public contract because this repository has no imported Cesium package assemblies. The unused `UnityEngine.EventSystems` import in `OrbitCamera` is satisfied by a namespace shim. This verifies C# and UI Toolkit API compatibility, not a full Unity project import/build, USS asset import or a rendered screen.

`TestHelpers.ps1` compiles and runs focused helper contracts using in-memory PlayerPrefs and a JSON adapter. It checks trail sampling/persistence/day changes/pruning/reset, XP thresholds, distance formatting and event route calculations. Unity native storage and JSON behavior still require an eventual Unity run.

Generated binaries and response files remain inside this ignored-by-Unity `Tests~` folder and are ignored by its local `.gitignore`.

`TestStyles.ps1` uses the installed Unity ExCSS tokenizer to check stylesheet syntax and verifies the referenced font paths. It intentionally accepts Unity-specific declaration names; semantic USS validation, asset URL resolution and rendered layout still require Unity import.

