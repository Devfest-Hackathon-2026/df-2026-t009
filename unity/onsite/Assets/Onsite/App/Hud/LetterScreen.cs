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
        public bool IsLiked => liked;
        public LetterScreen(OnsiteApp app, Drop drop, Callable.OpenDropResponse opened)
        {
            this.app = app; Drop = drop;
            AddToClassList("letter-screen");
            var panel = new HudPanel("FIELD LETTER", "この場所でひらいた", app.ShowMap); Add(panel);
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("screen-scroll"); panel.Body.Add(scroll);
            scroll.Add(UiKit.Author(drop.authorName, AppFormat.Ago(drop.createdAt)));
            scroll.Add(UiKit.Label(AppFormat.Coordinates(drop.lat, drop.lng), "mono letter-location"));
            if (!string.IsNullOrEmpty(opened.photo))
            {
                photo = DecodePhoto(opened.photo);
                if (photo != null)
                {
                    var image = new Image { image = photo, scaleMode = ScaleMode.ScaleToFit };
                    image.AddToClassList("letter-photo");
                    // 元の縦横比で写真を大きく見せる。
                    image.RegisterCallback<GeometryChangedEvent>(e =>
                    {
                        if (photo != null && e.newRect.width > 1 && Math.Abs(e.newRect.width - e.oldRect.width) > 0.5f)
                            image.style.height = e.newRect.width * photo.height / photo.width;
                    });
                    var media = new VisualElement(); media.AddToClassList("letter-media"); media.Add(image); scroll.Add(media);
                    scroll.Add(UiKit.Label("FIELD PHOTO / この場所の景色", "mono photo-caption"));
                }
                else app.Toast("写真を読み込めませんでした");
            }
            scroll.Add(UiKit.Label(drop.teaser, "letter-title"));
            scroll.Add(UiKit.Label(opened.body ?? "", "letter-body body-copy"));
            scroll.Add(UiKit.Rule());
            liked = app.Api?.Liked.Any(l => l.id == drop.id) == true;
            like = UiKit.Button("", () => app.Like(this)); UpdateLike();
            var reactions = UiKit.Row(like, new MetricChip("footprint", drop.openCount.ToString()),
                new MetricChip("comment", (drop.commentCount ?? 0).ToString()));
            reactions.AddToClassList("letter-reactions"); reactions.AddToClassList("inline-metrics"); scroll.Add(reactions);
            scroll.Add(UiKit.Label("GUESTBOOK / ここに来た人の寄せ書き", "section-label"));
            comments = new VisualElement(); scroll.Add(comments);
            comments.Add(UiKit.Label("寄せ書きを取得しています…", "muted"));
            input = UiKit.Field("ひとこと残す", 200, true); scroll.Add(input);
            input.RegisterValueChangedCallback(_ => UpdateSend());
            send = UiKit.Button("SEND / 寄せ書きを送る", () => app.Comment(this, input.value), true); scroll.Add(send);
            if (drop.authorId == app.Uid)
            {
                delete = UiKit.Button("この投稿を削除", () => app.DeleteLetter(this)); delete.AddToClassList("danger"); scroll.Add(delete);
            }
            var close = UiKit.Button("CLOSE / とじる", app.ShowMap); close.AddToClassList("hint-action"); scroll.Add(close); UpdateSend();
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
                row.Add(UiKit.Author(item.authorName, AppFormat.Ago(item.createdAt)));
                row.Add(UiKit.Label(item.text, "body-copy")); comments.Add(row);
            }
        }
        void UpdateLike()
        {
            like.text = liked ? "LIKED / いいね済み" : "LIKE / いいね";
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
