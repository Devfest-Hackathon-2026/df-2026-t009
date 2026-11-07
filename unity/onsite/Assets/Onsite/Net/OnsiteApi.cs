using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Onsite.Net
{
    [DisallowMultipleComponent]
    public sealed class OnsiteApi : MonoBehaviour
    {
        [SerializeField] private FirebaseConfig config = null;
        public static OnsiteApi Instance { get; private set; }

        public FirebaseAuthRest Auth { get; private set; }
        public Callable Functions { get; private set; }
        public FirestoreRest Firestore { get; private set; }
        public Storage Storage { get; private set; }

        public IReadOnlyList<Drop> Drops { get; private set; } = Array.Empty<Drop>();
        public IReadOnlyList<OpenedEntry> Opened { get; private set; } = Array.Empty<OpenedEntry>();
        public IReadOnlyList<PlacedEntry> Placed { get; private set; } = Array.Empty<PlacedEntry>();
        public IReadOnlyList<Notice> Notices { get; private set; } = Array.Empty<Notice>();
        public IReadOnlyList<LikedEntry> Liked { get; private set; } = Array.Empty<LikedEntry>();

        public event Action DropsChanged;
        public event Action MyLogChanged;
        public event Action NoticesChanged;
        public event Action<string> Error;

        private Coroutine polling;
        private bool refreshing;
        private bool autoSignIn = true;
        private int identityVersion;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            if (config == null)
            {
                Report("OnsiteApi に FirebaseConfig を設定してください");
                enabled = false;
                return;
            }
            Instance = this;
            Auth = new FirebaseAuthRest(config);
            Functions = new Callable(config, Auth);
            Firestore = new FirestoreRest(config, Auth);
            Storage = new Storage(config, Auth);
            Auth.Error += Report;
            Auth.SignedOut += ForgetPrivateData;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (Instance == this && Auth != null) polling = StartCoroutine(Poll());
        }

        private void OnDisable()
        {
            if (polling != null) StopCoroutine(polling);
            polling = null;
            refreshing = false;
        }

        private void OnDestroy()
        {
            if (Auth != null)
            {
                Auth.Error -= Report;
                Auth.SignedOut -= ForgetPrivateData;
            }
            if (Instance == this) Instance = null;
        }

        private IEnumerator Poll()
        {
            while (true)
            {
                yield return Refresh();
                yield return new WaitForSecondsRealtime(6f);
            }
        }

        public IEnumerator EnsureSignedIn()
        {
            autoSignIn = true;
            yield return Auth.EnsureSignedIn();
        }

        public IEnumerator GetIdToken(Action<string> done) => Auth.GetIdToken(done);

        public void SignOutAndForget() => Auth.SignOutAndForget();

        public IEnumerator Refresh()
        {
            if (refreshing)
            {
                while (refreshing) yield return null;
                yield break;
            }
            refreshing = true;
            try
            {
                if (autoSignIn || Auth.IsSignedIn) yield return Auth.EnsureSignedIn();
                yield return Firestore.ListDrops(300, items =>
                {
                    Drops = items;
                    DropsChanged?.Invoke();
                }, Report);

                if (!Auth.IsSignedIn) yield break;
                var uid = Auth.Uid;
                var version = identityVersion;
                var logChanged = false;
                yield return Firestore.ListOwn<OpenedEntry>(uid, "opened", "openedAt", items =>
                {
                    if (!Current(uid, version)) return;
                    Opened = items;
                    logChanged = true;
                }, message => { if (Current(uid, version)) Report(message); });
                if (!Current(uid, version)) yield break;
                yield return Firestore.ListOwn<PlacedEntry>(uid, "placed", "createdAt", items =>
                {
                    if (!Current(uid, version)) return;
                    Placed = items;
                    logChanged = true;
                }, message => { if (Current(uid, version)) Report(message); });
                if (!Current(uid, version)) yield break;
                yield return Firestore.ListOwn<Notice>(uid, "notifications", "createdAt", items =>
                {
                    if (!Current(uid, version)) return;
                    Notices = items;
                    NoticesChanged?.Invoke();
                }, message => { if (Current(uid, version)) Report(message); });
                if (!Current(uid, version)) yield break;
                yield return Firestore.ListOwn<LikedEntry>(uid, "liked", "createdAt", items =>
                {
                    if (!Current(uid, version)) return;
                    Liked = items;
                    logChanged = true;
                }, message => { if (Current(uid, version)) Report(message); });
                if (Current(uid, version) && logChanged) MyLogChanged?.Invoke();
            }
            finally { refreshing = false; }
        }

        private bool Current(string uid, int version) => identityVersion == version && Auth.Uid == uid;

        private void ForgetPrivateData()
        {
            // 古い HTTP 応答が、消した履歴を戻さないように世代も更新する。
            identityVersion++;
            autoSignIn = false;
            Opened = Array.Empty<OpenedEntry>();
            Placed = Array.Empty<PlacedEntry>();
            Liked = Array.Empty<LikedEntry>();
            Notices = Array.Empty<Notice>();
            MyLogChanged?.Invoke();
            NoticesChanged?.Invoke();
        }

        private void Report(string message) => Rest.Fail(Error, message);

        public IEnumerator CreateDrop(Callable.CreateDropRequest input, Action<Callable.CreateDropResponse> done,
            Action<string> error = null) => Functions.CreateDrop(input, done, error ?? Report);

        public IEnumerator OpenDrop(Callable.OpenDropRequest input, Action<Callable.OpenDropResponse> done,
            Action<string> error = null) => Functions.OpenDrop(input, done, error ?? Report);

        public IEnumerator AddComment(Callable.AddCommentRequest input, Action<Callable.AddCommentResponse> done,
            Action<string> error = null) => Functions.AddComment(input, done, error ?? Report);

        public IEnumerator ToggleLike(Callable.ToggleLikeRequest input, Action<Callable.ToggleLikeResponse> done,
            Action<string> error = null) => Functions.ToggleLike(input, done, error ?? Report);

        public IEnumerator DeleteDrop(Callable.DeleteDropRequest input, Action<Callable.DeleteDropResponse> done,
            Action<string> error = null) => Functions.DeleteDrop(input, done, error ?? Report);

        public IEnumerator CreateEvent(Callable.CreateEventRequest input, Action<Callable.CreateEventResponse> done,
            Action<string> error = null) => Functions.CreateEvent(input, done, error ?? Report);

        public IEnumerator JoinEvent(Callable.JoinEventRequest input, Action<Callable.JoinEventResponse> done,
            Action<string> error = null) => Functions.JoinEvent(input, done, error ?? Report);

        public IEnumerator DeleteEvent(Callable.DeleteEventRequest input, Action<Callable.DeleteEventResponse> done,
            Action<string> error = null) => Functions.DeleteEvent(input, done, error ?? Report);

        public IEnumerator AddFootprints(Callable.AddFootprintsRequest input, Action<Callable.AddFootprintsResponse> done,
            Action<string> error = null) => Functions.AddFootprints(input, done, error ?? Report);

        public IEnumerator UploadJpeg(byte[] jpeg, Action<string> done, Action<string> error = null) =>
            Storage.UploadJpeg(jpeg, done, error ?? Report);

        public IEnumerator ListDrops(int limit, Action<Drop[]> done, Action<string> error = null) =>
            Firestore.ListDrops(limit, done, error ?? Report);

        public IEnumerator GetProfile(string uid, Action<Profile> done, Action<string> error = null) =>
            Firestore.GetProfile(uid, done, error ?? Report);

        public IEnumerator ListOwn<T>(string uid, string sub, string orderField, Action<T[]> done,
            Action<string> error = null) => Firestore.ListOwn(uid, sub, orderField, done, error ?? Report);

        public IEnumerator ListComments(string dropId, Action<Comment[]> done, Action<string> error = null) =>
            Firestore.ListComments(dropId, done, error ?? Report);

        public IEnumerator ListEvents(Action<KokoEvent[]> done, Action<string> error = null) =>
            Firestore.ListEvents(done, error ?? Report);

        public IEnumerator ListFootprints(double minLat, double maxLat, Action<Footprint[]> done,
            Action<string> error = null) => Firestore.ListFootprints(minLat, maxLat, done, error ?? Report);
    }
}