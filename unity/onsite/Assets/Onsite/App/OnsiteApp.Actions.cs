using System;
using System.Collections;
using System.Linq;
using Onsite.App.Hud;
using Onsite.App.Ui;
using Onsite.Map;
using Onsite.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App
{
    public sealed partial class OnsiteApp
    {
        bool sendingFootprints;
        string activeRoute;

        bool CanRequest()
        {
            if (Api == null || Api.Auth == null) { Toast("通信の準備ができていません"); return false; }
            if (string.IsNullOrEmpty(Nickname)) { Toast("最初にニックネームを設定してください"); return false; }
            if (!Api.Auth.IsSignedIn) { Toast("接続中です。少し待ってからお試しください"); return false; }
            return !Busy;
        }
        IEnumerator RefreshMetadata()
        {
            if (Api == null || Api.Auth == null || refreshingMeta) yield break;
            refreshingMeta = true;
            try
            {
                var uid = Uid;
                var version = identityVersion;
                if (!string.IsNullOrEmpty(uid))
                {
                    yield return Api.GetProfile(uid, p => { if (Uid == uid && version == identityVersion) Profile = p; });
                    yield return Api.ListOwn<JoinedEventEntry>(uid, "events", "joinedAt", entries =>
                    {
                        if (Uid != uid || version != identityVersion) return;
                        JoinedIds.Clear(); foreach (var e in entries) JoinedIds.Add(e.id);
                    });
                }
                yield return Api.ListEvents(items => { Events.Clear(); Events.AddRange(items); });
                hud?.Refresh(); RefreshScreen(); CheckCompletions();
            }
            finally { refreshingMeta = false; }
        }
        public void RefreshEvents() { if (Api != null && !refreshingMeta) StartCoroutine(RefreshMetadata()); }

        public void OpenSelected()
        {
            if (Selected == null || !CanRequest()) return;
            if (StateOf(Selected) == DropState.Locked) { Toast("範囲内に近づくとひらけます"); return; }
            StartCoroutine(OpenRoutine(Selected));
        }
        IEnumerator OpenRoutine(Drop drop)
        {
            var version = identityVersion; var requestUid = Uid;
            Busy = true; hud.Refresh();
            Sound.StartLoop("verify");
            Callable.OpenDropResponse result = null;
            try
            {
                yield return Api.OpenDrop(new Callable.OpenDropRequest
                {
                    dropId = drop.id, lat = HasLocation ? Lat : (double?)null,
                    lng = HasLocation ? Lng : (double?)null,
                    accuracy = HasLocation ? Accuracy : (double?)null, name = Nickname
                }, value => result = value, Toast);
            }
            finally
            {
                Sound.StopLoop("verify");
                if (version == identityVersion) Busy = false; hud.Refresh();
            }
            if (result == null || version != identityVersion || requestUid != Uid) yield break;
            if (result.status == "too_far")
            {
                Toast("まだ遠いようです。半径 " + AppFormat.Distance(result.radius) + " まで近づいてください");
                yield break;
            }
            // 本文・写真は必ずサーバーの開封成功後にだけ画面へ渡す。
            if (result.status != "opened") { Toast("開封できませんでした。もう一度お試しください"); yield break; }
            if (drop.authorId != Uid) OpenedIds.Add(drop.id); UpdateStates();
            if (result.firstTime)
            {
                playingSequence = true;
                bool finished = false;
                sequence.Play(false, () => finished = true);
                // 万一演出が止まっても、3秒で必ず本文へ進む
                float started = Time.unscaledTime;
                while (!finished && Time.unscaledTime - started < 3f) yield return null;
                playingSequence = false;
            }
            if (version != identityVersion || requestUid != Uid) yield break;
            var letter = new LetterScreen(this, drop, result);
            Screen("letter", letter);
            nextComments = Time.unscaledTime + 6;
            LoadComments(letter);
            yield return Api.Refresh();
            RefreshEvents();
        }
        void LoadComments(LetterScreen letter)
        {
            StartCoroutine(Api.ListComments(letter.Drop.id, items =>
            {
                if (currentScreen == letter) letter.SetComments(items);
            }));
        }
        public void Like(LetterScreen letter)
        {
            if (CanRequest())
            {
                if (!letter.IsLiked) Sound.SuppressTap();
                StartCoroutine(LikeRoutine(letter));
            }
        }
        IEnumerator LikeRoutine(LetterScreen letter)
        {
            var version = identityVersion; var requestUid = Uid;
            Busy = true; letter.SetBusy(true);
            try
            {
                yield return Api.ToggleLike(new Callable.ToggleLikeRequest { dropId = letter.Drop.id, name = Nickname },
                    result =>
                    {
                        if (version != identityVersion || requestUid != Uid) return;
                        letter.SetLiked(result.liked);
                        if (result.liked) Sound.Play("like");
                    }, Toast);
            }
            finally { if (version == identityVersion) Busy = false; letter.SetBusy(false); }
            yield return Api.Refresh();
        }
        public void Comment(LetterScreen letter, string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || text.Length > 200 || !CanRequest()) return;
            Sound.SuppressTap();
            StartCoroutine(CommentRoutine(letter, text));
        }
        IEnumerator CommentRoutine(LetterScreen letter, string text)
        {
            var version = identityVersion; var requestUid = Uid;
            Busy = true; letter.SetBusy(true);
            bool success = false;
            try
            {
                yield return Api.AddComment(new Callable.AddCommentRequest { dropId = letter.Drop.id, name = Nickname, text = text }, _ => success = true, Toast);
            }
            finally { if (version == identityVersion) Busy = false; letter.SetBusy(false); }
            if (success && version == identityVersion && requestUid == Uid)
            {
                Sound.Play("send");
                letter.ClearComment(); LoadComments(letter); RefreshEvents();
            }
        }
        public void DeleteLetter(LetterScreen letter)
        {
            if (letter.Drop.authorId != Uid || !CanRequest()) return;
            Confirm("この投稿を消しますか", "本文・写真・寄せ書きも削除されます。元には戻せません。", () => StartCoroutine(DeleteRoutine(letter)));
        }
        IEnumerator DeleteRoutine(LetterScreen letter)
        {
            var version = identityVersion; var requestUid = Uid;
            Busy = true; letter.SetBusy(true);
            bool success = false;
            try { yield return Api.DeleteDrop(new Callable.DeleteDropRequest { dropId = letter.Drop.id }, r => success = r.ok, Toast); }
            finally { if (version == identityVersion) Busy = false; letter.SetBusy(false); }
            if (success && version == identityVersion && requestUid == Uid) { ClearSelection(); ShowMap(); ToastInfo("投稿を削除しました"); yield return Api.Refresh(); }
        }

        public void BeginPost()
        {
            if (!CanRequest()) return;
            if (!HasLocation || World == null || !World.HasOrigin) { Toast("現在地が分かってから投稿できます。設定からデモでも試せます"); return; }
            ShowMap(); ClearSelection(); placing = true;
            hud.SetPlacement(true);
        }
        public void CancelPlacement() { placing = false; hud.SetPlacement(false); }
        public void ConfirmPlacement()
        {
            if (!placing) return;
            if (!TryGround(new Vector2(UnityEngine.Screen.width * 0.5f, UnityEngine.Screen.height * 0.5f), out var lat, out var lng))
            { Toast("地面に照準を合わせてください"); return; }
            placing = false; hud.SetPlacement(false);
            Screen("post", new PostScreen(this, lat, lng));
        }
        public void TakePhoto() => bridge?.TakePhoto();
        void PhotoReady(byte[] bytes)
        {
            if (currentScreen is PostScreen post)
            {
                if (bytes == null || bytes.Length == 0) Toast("写真を取得できませんでした。この端末の撮影機能を確認してください");
                else post.SetPhoto(bytes);
            }
        }
        public void SubmitPost(PostScreen post, string teaser, string body, int radius, byte[] jpeg)
        {
            teaser = (teaser ?? "").Trim(); body = (body ?? "").Trim();
            if (!CanRequest() || teaser.Length == 0 || teaser.Length > 60 || body.Length > 600 ||
                (body.Length == 0 && (jpeg == null || jpeg.Length == 0)) || !new[] {100,300,500,1000}.Contains(radius)) return;
            Sound.SuppressTap();
            StartCoroutine(PostRoutine(post, teaser, body, radius, jpeg));
        }
        IEnumerator PostRoutine(PostScreen post, string teaser, string body, int radius, byte[] jpeg)
        {
            var version = identityVersion; var requestUid = Uid;
            Busy = true; post.SetBusy(true);
            string photoPath = null; bool success = false;
            try
            {
                if (jpeg != null && jpeg.Length > 0)
                {
                    yield return Api.UploadJpeg(jpeg, value => photoPath = value, Toast);
                    if (string.IsNullOrEmpty(photoPath) || version != identityVersion || requestUid != Uid) yield break;
                }
                yield return Api.CreateDrop(new Callable.CreateDropRequest
                {
                    teaser = teaser, body = body, radius = radius, lat = post.Lat, lng = post.Lng,
                    authorName = Nickname, photoPath = photoPath
                }, _ => success = true, Toast);
            }
            finally { if (version == identityVersion) Busy = false; post.SetBusy(false); }
            if (success && version == identityVersion && requestUid == Uid)
            {
                ShowMap(); ToastInfo("ビーコンを置きました  +15 EXP");
                Sound.Play("send");
                yield return Api.Refresh(); RefreshEvents();
            }
        }

        public void JoinEvent(KokoEvent ev, bool join, Action<bool> done = null)
        {
            if (CanRequest()) StartCoroutine(JoinRoutine(ev, join, done)); else done?.Invoke(false);
        }
        IEnumerator JoinRoutine(KokoEvent ev, bool join, Action<bool> done)
        {
            var version = identityVersion; var requestUid = Uid;
            Busy = true; bool success = false;
            try
            {
                yield return Api.JoinEvent(new Callable.JoinEventRequest { eventId = ev.id, join = join }, r =>
                {
                    if (version != identityVersion || requestUid != Uid) return;
                    if (r.joined) JoinedIds.Add(ev.id); else JoinedIds.Remove(ev.id);
                    success = true;
                }, Toast);
            }
            finally { if (version == identityVersion) Busy = false; }
            if (success && version == identityVersion && requestUid == Uid)
            {
                if (join) { activeRoute = ev.id; paths?.SetRoute(ev.stops); }
                else if (activeRoute == ev.id) ClearRoute();
                ToastInfo(join ? "参加しました。地図にルートを表示します" : "参加をやめました");
                RefreshScreen(); CheckCompletions();
            }
            done?.Invoke(success && version == identityVersion && requestUid == Uid);
        }
        public void CreateEvent(string title, string description, string[] ids, Action<bool> done = null) => CreateEvent(title, description, ids, null, null, done);
        public void CreateEvent(string title, string description, string[] ids, DateTime? startAt, DateTime? endAt, Action<bool> done = null)
        {
            title = (title ?? "").Trim(); description = (description ?? "").Trim();
            if (!CanRequest() || title.Length == 0 || title.Length > 40 || description.Length > 300 || ids == null || ids.Length < 2 || ids.Length > 20 || ids.Distinct().Count() != ids.Length)
            { Toast("イベント名と2〜20か所の巡る順序を確認してください"); done?.Invoke(false); return; }
            StartCoroutine(CreateEventRoutine(title, description, ids, startAt, endAt, done));
        }
        static long? Millis(DateTime? t) => t.HasValue ? new DateTimeOffset(t.Value).ToUnixTimeMilliseconds() : (long?)null;
        IEnumerator CreateEventRoutine(string title, string description, string[] ids, DateTime? startAt, DateTime? endAt, Action<bool> done)
        {
            var version = identityVersion; var requestUid = Uid;
            Busy = true; bool success = false;
            try
            {
                yield return Api.CreateEvent(new Callable.CreateEventRequest { title = title, description = description, dropIds = ids, name = Nickname, startAt = Millis(startAt), endAt = Millis(endAt) }, _ => success = true, Toast);
            }
            finally { if (version == identityVersion) Busy = false; }
            if (success && version == identityVersion && requestUid == Uid)
            {
                ToastInfo("イベントを作りました");
                yield return RefreshMetadata(); RefreshScreen();
            }
            done?.Invoke(success && version == identityVersion && requestUid == Uid);
        }
        public void ShowRoute(KokoEvent ev)
        {
            activeRoute = ev.id; paths?.SetRoute(ev.stops); ShowMap(); hud.Refresh();
        }
        public KokoEvent ActiveRoute => Events.FirstOrDefault(e => e.id == activeRoute);
        public void ClearRoute() { activeRoute = null; paths?.SetRoute(null); hud?.Refresh(); }
        void CheckCompletions()
        {
            foreach (var ev in Events)
            {
                if (!JoinedIds.Contains(ev.id) || ev.stops == null || ev.stops.Length == 0 || !ev.stops.All(s => OpenedIds.Contains(s.id))) continue;
                var key = "onsite.complete." + Uid + "." + ev.id;
                if (completionShown.Contains(key)) continue;
                completionShown.Add(key); PlayerPrefs.SetString("onsite.completedEvents", string.Join("\n", completionShown)); PlayerPrefs.Save(); completions.Enqueue(ev.title);
            }
        }
        void PlayComplete(string title)
        {
            playingSequence = true;
            sequence.Play(true, () => playingSequence = false, title);
        }
        IEnumerator FlushFootprints()
        {
            if (!ShareFootprints || DemoMode || Api == null || sendingFootprints) yield break;
            sendingFootprints = true;
            var batch = pendingFootprints.Take(60).ToArray();
            pendingFootprints.RemoveRange(0, batch.Length);
            bool success = false;
            try
            {
                yield return Api.AddFootprints(new Callable.AddFootprintsRequest { points = batch }, _ => success = true, Toast);
                if (!success && ShareFootprints && !DemoMode)
                    pendingFootprints.InsertRange(0, batch.Take(Math.Max(0, 120 - pendingFootprints.Count)));
            }
            finally { sendingFootprints = false; }
        }
    }
}





