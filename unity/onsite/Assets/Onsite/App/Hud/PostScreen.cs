using System;
using System.Collections.Generic;
using Onsite.App.Ui;
using Onsite.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Hud
{
    public sealed class PostScreen : VisualElement, IDisposable
    {
        readonly OnsiteApp app;
        readonly TextField teaser, body;
        readonly Label count;
        readonly Image preview;
        readonly Button submit, shutter, remove;
        readonly List<Button> radiusButtons = new List<Button>();
        byte[] jpeg;
        Texture2D photo;
        int radius = 100;
        bool busy;
        public double Lat { get; }
        public double Lng { get; }
        public PostScreen(OnsiteApp app, double lat, double lng)
        {
            this.app = app; Lat = lat; Lng = lng;
            AddToClassList("post-screen");
            var panel = new HudPanel("PLACE SIGNAL", "ここに発見を残す", app.ShowMap); Add(panel);
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("screen-scroll"); panel.Body.Add(scroll);
            scroll.Add(UiKit.Label(AppFormat.Coordinates(lat, lng), "mono"));
            if (app.HasLocation && GeoMath.Distance(app.Lat, app.Lng, lat, lng) < 30)
                scroll.Add(UiKit.Label("現在地から30 m以内です。自宅や職場など、知られたくない場所には置かないでください。", "privacy-warning"));
            teaser = UiKit.Field("予告 / 誰でも見える", 60); scroll.Add(teaser);
            body = UiKit.Field("本文 / 近づいた人だけ読める", 600, true); scroll.Add(body);
            count = UiKit.Label("0 / 60    0 / 600", "mono caption"); scroll.Add(count);
            teaser.RegisterValueChangedCallback(_ => Validate()); body.RegisterValueChangedCallback(_ => Validate());
            scroll.Add(UiKit.Label("OPEN RADIUS / ひらける範囲", "section-label"));
            var radii = new VisualElement(); radii.AddToClassList("segmented");
            foreach (int value in new[] { 100, 300, 500, 1000 })
            {
                var button = UiKit.Button(AppFormat.Distance(value), () => { radius = value; UpdateRadius(); });
                button.AddToClassList("mono"); radiusButtons.Add(button); radii.Add(button);
            }
            scroll.Add(radii); UpdateRadius();
            scroll.Add(UiKit.Label("PHOTO / その場の景色", "section-label"));
            preview = new Image { scaleMode = ScaleMode.ScaleToFit }; preview.AddToClassList("post-preview"); preview.style.display = DisplayStyle.None; scroll.Add(preview);
            shutter = UiKit.Button("写真を撮る", app.TakePhoto);
            remove = UiKit.Button("写真を外す", () => { jpeg = null; Dispose(); preview.image = null; preview.style.display = DisplayStyle.None; Validate(); });
            scroll.Add(UiKit.Row(shutter, remove));
            submit = UiKit.Button("この場所に置く", () => app.SubmitPost(this, teaser.value, body.value, radius, jpeg), true); scroll.Add(submit);
            scroll.Add(UiKit.Button("場所を選び直す", app.BeginPost));
            Validate();
        }
        void UpdateRadius()
        {
            var values = new[] {100,300,500,1000};
            for (int i = 0; i < values.Length; i++) radiusButtons[i].EnableInClassList("selected", values[i] == radius);
        }
        void Validate()
        {
            count.text = (teaser.value?.Length ?? 0) + " / 60    " + (body.value?.Length ?? 0) + " / 600";
            submit?.SetEnabled(!busy && !string.IsNullOrWhiteSpace(teaser.value) && (!string.IsNullOrWhiteSpace(body.value) || jpeg != null));
            remove.SetEnabled(!busy && jpeg != null);
        }
        public void SetPhoto(byte[] bytes)
        {
            if (busy) return;
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!texture.LoadImage(bytes)) { UnityEngine.Object.Destroy(texture); app.Toast("写真の形式を確認してください"); return; }
            // 大きいカメラ画像は送信前に縮小し、回線と端末メモリを守る。
            var scale = Math.Min(1f, 1600f / Math.Max(texture.width, texture.height));
            var target = RenderTexture.GetTemporary(Math.Max(1, (int)(texture.width * scale)), Math.Max(1, (int)(texture.height * scale)), 0);
            var previous = RenderTexture.active;
            Texture2D reduced = null;
            try
            {
                Graphics.Blit(texture, target); RenderTexture.active = target;
                reduced = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                reduced.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); reduced.Apply();
                var encoded = reduced.EncodeToJPG(85);
                Dispose(); photo = reduced; reduced = null; jpeg = encoded;
                preview.image = photo; preview.style.display = DisplayStyle.Flex;
            }
            finally
            {
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(texture); if (reduced != null) UnityEngine.Object.Destroy(reduced);
            }
            Validate();
        }
        public void SetBusy(bool value)
        {
            busy = value; teaser.SetEnabled(!value); body.SetEnabled(!value); shutter.SetEnabled(!value);
            foreach (var button in radiusButtons) button.SetEnabled(!value);
            submit.text = value ? "送信中…" : "この場所に置く"; Validate();
        }
        public void Dispose() { if (photo != null) UnityEngine.Object.Destroy(photo); photo = null; }
    }
}
