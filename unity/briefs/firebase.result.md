Implemented within the two allowed folders.

- `FirebaseConfig` and `FirebaseAuthRest`: editor configuration, anonymous sign-in, token refresh, persistence, sign-out.
- `Callable`: all nine typed function wrappers.
- `FirestoreRest`, models, and JSON helpers: read queries and typed-value decoding.
- `Storage`: JPEG uploads.
- `DeviceBridge` and `OnsiteBridge.jslib`: location and photo handling.
- `OnsiteApi`: singleton facade, polling, caches, and change events.

Validation passed: warning-free compilation for Editor/WebGL/Android, 127 C# assertions, and JavaScript bridge tests. Unity was not launched; live Firebase and hardware remain untested.

Setup instructions are in `Net/README.md`. Separate concurrent changes appeared in `.gitignore` and `firebase.json`; I did not edit them.