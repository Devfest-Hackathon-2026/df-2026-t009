# Task: Firebase REST client + WebGL bridges for the Unity app "onsite"

You are writing C# (Unity 6000.3, .NET Standard 2.1, no external NuGet) and one `.jslib` file.
Write ONLY under `unity/onsite/Assets/Onsite/Net/` and `unity/onsite/Assets/Plugins/WebGL/`.
Do not touch anything else in the repo. Do not run Unity. Do not put any API key in code.

## Context
The backend already exists (Firebase project `kokodake-t009`, region `asia-northeast1`). Read these to understand the API exactly:
- `functions/src/index.ts` (callable functions and their inputs/outputs)
- `firestore.rules` (what the client may read)
- `web/src/firebase.ts`, `web/src/useData.ts`, `web/src/types.ts` (how the web client uses it)

The Unity app must use plain HTTPS (UnityWebRequest) because the Firebase Unity SDK does not support WebGL.

## Deliverables

1. `Assets/Onsite/Net/FirebaseConfig.cs`
   - `[CreateAssetMenu]` ScriptableObject with fields: `apiKey`, `projectId`, `region` (default "asia-northeast1"), `storageBucket`.
   - Values are filled in the editor; never hard-code them.

2. `Assets/Onsite/Net/FirebaseAuthRest.cs`
   - Anonymous sign-up: `POST https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=API_KEY` body `{"returnSecureToken":true}`.
   - Refresh: `POST https://securetoken.googleapis.com/v1/token?key=API_KEY` form `grant_type=refresh_token&refresh_token=...`.
   - Persist `uid` and `refreshToken` in `PlayerPrefs` (keys `onsite.uid`, `onsite.refresh`), restore on start, refresh the ID token when it is within 5 minutes of expiry.
   - Public API: `IEnumerator EnsureSignedIn()`, `string Uid`, `IEnumerator GetIdToken(Action<string> done)`, `void SignOutAndForget()`.

3. `Assets/Onsite/Net/Callable.cs`
   - Calls `https://{region}-{projectId}.cloudfunctions.net/{name}` with POST JSON `{"data": ...}` and header `Authorization: Bearer <idToken>`.
   - Parses `{"result": ...}` or `{"error": {"status","message"}}` and surfaces the error message (Japanese messages come from the server; pass them through).
   - Typed wrappers with `[Serializable]` request/response classes for: `createDrop`, `openDrop`, `addComment`, `toggleLike`, `deleteDrop`, `createEvent`, `joinEvent`, `deleteEvent`, `addFootprints`. Match field names in `functions/src/index.ts` exactly. `openDrop` returns `status` ("opened" | "too_far"), `firstTime`, `distance`, `radius`, `body`, `photo` (data URL or null).

4. `Assets/Onsite/Net/FirestoreRest.cs`
   - Read-only helpers against `https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents`.
   - `ListDrops(limit)` → use `:runQuery` with `orderBy createdAt desc`.
   - `GetProfile(uid)`, `ListOwn(uid, sub, orderField)` for `users/{uid}/opened|placed|notifications|liked|events`, `ListComments(dropId)`, `ListEvents()`, `ListFootprints(minLat, maxLat)` (range on `lat`).
   - Send the ID token as Bearer when available (rules need it for `users/*` and comments).
   - Convert Firestore typed values (`stringValue`, `integerValue` (string!), `doubleValue`, `booleanValue`, `timestampValue`, `arrayValue`, `mapValue`, `nullValue`) into plain C# models. Write a small, robust parser; Unity's JsonUtility cannot parse these, so use a minimal hand-written JSON parser (MiniJSON-style, written by you, MIT-compatible original code).
   - Models in `Assets/Onsite/Net/Models.cs`: `Drop`, `OpenedEntry`, `PlacedEntry`, `Profile`, `Comment`, `Notice`, `KokoEvent` (with `EventStop[] stops`), `Footprint`. Mirror `web/src/types.ts`. Timestamps as `DateTime` (UTC) nullable.

5. `Assets/Onsite/Net/Storage.cs`
   - Upload a JPEG byte[] to `uploads/{uid}/{guid}.jpg` via `https://firebasestorage.googleapis.com/v0/b/{bucket}/o?uploadType=media&name=...` with Bearer token and `Content-Type: image/jpeg`. Return the object path.

6. `Assets/Plugins/WebGL/OnsiteBridge.jslib` + `Assets/Onsite/Net/DeviceBridge.cs`
   - Geolocation: `watchPosition` (high accuracy) sending `lat,lng,accuracy` to a GameObject named `OnsiteBridge` via `SendMessage(name, "OnGeo", "lat,lng,acc")` and errors via `OnGeoError`.
   - Photo: open a hidden `<input type=file accept=image/* capture=environment>`, downscale to max 1600px JPEG 0.82 on a canvas, send base64 to `OnPhoto`.
   - `DeviceBridge.cs` (MonoBehaviour, put on GameObject `OnsiteBridge`): on WebGL use the jslib; on Android/Editor use `Input.location` (request permission on Android via `UnityEngine.Android.Permission`) and `NativeCamera`-free fallback (on Android just return null for photo for now, TODO comment). Exposes C# events `OnLocation(double lat, double lng, float acc)` and `OnPhotoReady(byte[] jpeg)`.
   - Use `#if UNITY_WEBGL && !UNITY_EDITOR` guards correctly.

7. `Assets/Onsite/Net/OnsiteApi.cs`
   - A single MonoBehaviour facade (singleton) that owns auth + callable + firestore + storage, with coroutine-based methods the UI can call, plus a simple polling loop (every 6 s) that refreshes drops and, when signed in, own opened/placed/notifications/liked. Raise C# events `DropsChanged`, `MyLogChanged`, `NoticesChanged`.

## Quality bar
- Compiles on Unity 6000.3 with no warnings you can avoid. No `async void`. Use coroutines + UnityWebRequest.
- Comments in Japanese, short, explaining why (match the style of `functions/src/index.ts`).
- Keep each file focused. No placeholder TODOs except the Android photo one.
- At the end, print a short summary of files and public APIs.
