using System;
using System.Linq;
using Onsite.App.Ui;
using Onsite.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Hud
{
    public sealed class LetterScreen : VisualElement, IDisposable
    {
        readonly OnsiteApp app;
        readonly VisualElement comments;
        readonly TextField input;
        readonly Button like, send, delete;
        Texture2D photo;
        bool liked, busy;
        public Drop Drop { get; }
        public LetterScreen(OnsiteApp app, Drop drop, Callable.OpenDropResponse opened)
        {
            this.app = app; Drop = drop;
            AddToClassList("letter-screen");
            var panel = new HudPanel("FIELD LETTER", "この場所でひらいた", app.ShowMap); Add(panel);
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("screen-scroll"); panel.Body.Add(scroll);
            scroll.Add(UiKit.Label(drop.authorName + " / " + AppFormat.Ago(drop.createdAt), "muted"));
            scroll.Add(UiKit.Label(drop.teaser, "letter-title"));
            if (!string.IsNullOrEmpty(opened.photo))
            {
                photo = DecodePhoto(opened.photo);
                if (photo != null)
                {
                    var image = new Image { image = photo, scaleMode = ScaleMode.ScaleToFit };
                    image.AddToClassList("letter-photo"); scroll.Add(image);
                }
                else app.Toast("写真を読み込めませんでした");
            }
            scroll.Add(UiKit.Label(opened.body ?? "", "letter-body body-copy"));
            liked = app.Api?.Liked.Any(l => l.id == drop.id) == true;
            like = UiKit.Button("", () => app.Like(this)); UpdateLike();
            scroll.Add(UiKit.Row(like, new MetricChip("footprint", drop.openCount.ToString())));
            scroll.Add(UiKit.Label("GUESTBOOK / ここに来た人の寄せ書き", "section-label"));
            comments = new VisualElement(); scroll.Add(comments);
            comments.Add(UiKit.Label("寄せ書きを取得しています…", "muted"));
            input = UiKit.Field("ひとこと残す", 200, true); scroll.Add(input);
            input.RegisterValueChangedCallback(_ => UpdateSend());
            send = UiKit.Button("寄せ書きを送る", () => app.Comment(this, input.value), true); scroll.Add(send);
            if (drop.authorId == app.Uid)
            {
                delete = UiKit.Button("この投稿を削除", () => app.DeleteLetter(this)); delete.AddToClassList("danger"); scroll.Add(delete);
            }
            scroll.Add(UiKit.Button("とじる", app.ShowMap)); UpdateSend();
        }
        static Texture2D DecodePhoto(string data)
        {
            Texture2D texture = null;
            try
            {
                if (!data.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) return null;
                var comma = data.IndexOf(',');
                if (comma < 0 || !data.Substring(0, comma).EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return null;
                var bytes = Convert.FromBase64String(data.Substring(comma + 1));
                texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (texture.LoadImage(bytes, true)) return texture;
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException) { }
            if (texture != null) UnityEngine.Object.Destroy(texture);
            return null;
        }
        public void SetComments(Comment[] items)
        {
            comments.Clear();
            if (items.Length == 0) comments.Add(UiKit.Label("最初のひとことを残してみよう。", "empty"));
            foreach (var item in items)
            {
                var row = new VisualElement(); row.AddToClassList("guestbook-entry");
                row.Add(UiKit.Label(item.authorName + " / " + AppFormat.Ago(item.createdAt), "muted"));
                row.Add(UiKit.Label(item.text, "body-copy")); comments.Add(row);
            }
        }
        void UpdateLike()
        {
            like.text = liked ? "いいねを取り消す" : "いいね";
            like.EnableInClassList("selected", liked);
        }
        public void SetLiked(bool value) { liked = value; UpdateLike(); }
        public void ClearComment() { input.value = ""; }
        public void SetBusy(bool value)
        {
            busy = value; like.SetEnabled(!value); delete?.SetEnabled(!value); input.SetEnabled(!value); UpdateSend();
        }
        void UpdateSend() => send?.SetEnabled(!busy && !string.IsNullOrWhiteSpace(input.value));
        public void Dispose() { if (photo != null) UnityEngine.Object.Destroy(photo); photo = null; }
    }
}
