# Onsite Firebase client

Assign a Firebase Config asset to the OnsiteApi component. Create the asset with
**Assets > Create > Onsite > Firebase Config**, and enter apiKey, projectId, region
and storageBucket in the Inspector. No config asset or credentials are included.

All C# types are in `Onsite.Net`. Put one OnsiteApi on a root GameObject; it persists
between scenes. Put DeviceBridge on a GameObject named `OnsiteBridge`.
Attach listeners before requesting location or selecting a photo.

## Public entry points

- `FirebaseAuthRest`: `EnsureSignedIn()`, `GetIdToken(done)`, `Uid`,
  `IsSignedIn`, `LastError`, `SignOutAndForget()`, `Error`, `SignedOut`.
  Only UID and refresh token are persisted. ID tokens refresh within five minutes
  of expiry. Explicit sign-out clears the local identity; it does not delete the
  server-side anonymous account.
- `Callable`: `CreateDrop`, `OpenDrop`, `AddComment`, `ToggleLike`,
  `DeleteDrop`, `CreateEvent`, `JoinEvent`, `DeleteEvent`, `AddFootprints`.
  Each takes its corresponding nested `*Request`, a typed `*Response` callback,
  and an optional `Action<string>` error callback. Event input dates are nullable
  UTC Unix milliseconds. Reopening without coordinates may return a null distance.
- `FirestoreRest`: `ListDrops(limit, done)`, `GetProfile(uid, done)`,
  `ListOwn<T>(uid, sub, orderField, done)`, `ListComments(dropId, done)`,
  `ListEvents(done)`, `ListFootprints(minLat, maxLat, done)`.
  All accept an optional error callback. Own collection types are OpenedEntry,
  PlacedEntry, Notice, LikedEntry and JoinedEventEntry. Query limits and ordering
  match the web client. A missing profile returns an empty Profile.
- `Storage.UploadJpeg(jpeg, done, error)`: uploads JPEG bytes smaller than 3MiB
  and returns the object path for `CreateDropRequest.photoPath`.
- `DeviceBridge`: `StartLocation()`, `StopLocation()`, `TakePhoto()`,
  `OnLocation(lat, lng, accuracy)`, `OnPhotoReady(jpeg)`, `LocationError`,
  `PhotoError`. Cancellation returns a null photo. Call TakePhoto directly from
  a UI gesture. Web location and camera selection need browser permissions and
  a secure context. Android and Editor use Input.location; photo selection
  currently returns null there.
- `OnsiteApi.Instance`: exposes Auth, Functions, Firestore and Storage, forwards
  their coroutine methods, and adds `Refresh()`. Polling refreshes public drops
  and the signed-in user's opened, placed, notifications and liked collections,
  waiting six real-time seconds between cycles. Subscribe to `DropsChanged`,
  `MyLogChanged`, `NoticesChanged` and `Error`; read the corresponding cached
  lists. Explicit sign-out clears private caches and keeps polling public data.

Example from a MonoBehaviour:

```csharp
StartCoroutine(Onsite.Net.OnsiteApi.Instance.OpenDrop(
    new Onsite.Net.Callable.OpenDropRequest
    {
        dropId = selectedId,
        lat = latitude,
        lng = longitude,
        accuracy = accuracy,
        name = nickname
    },
    result => ShowOpenedDrop(result),
    message => ShowError(message)));
```

The ShowOpenedDrop/ShowError methods and input variables in this example belong
to the calling UI.

## Verification without launching Unity

Run `Tests~/RunChecks.ps1` in PowerShell. Optional parameters `-UnityData`,
`-Dotnet` and `-Node` select local installations.

The script compiles the Editor, WebGL and Android branches against the installed
Unity 6000.3 assemblies and .NET Standard 2.1 with warnings as errors. It then
runs C# HTTP/auth/model/facade contract tests with test doubles and Node tests
for the JavaScript bridge. Generated binaries are removed. The Tests~ directory
is ignored by Unity's asset importer.

These checks do not exercise real Firebase credentials, browser permissions,
WebGL linking, or device hardware.