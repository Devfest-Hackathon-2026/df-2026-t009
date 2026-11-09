using System;
using System.Linq;
using Onsite.App.Ui;
using Onsite.Net;
using UnityEngine.UIElements;

namespace Onsite.App.Hud
{
    public sealed class MeScreen : VisualElement
    {
        private readonly OnsiteApp app;
        private readonly VisualElement stats;
        private readonly VisualElement log;
        private readonly Label distance;
        private readonly Toggle demo;
        private readonly Toggle sharing;
        private readonly Toggle sound;
        private readonly Toggle haptics;
        public MeScreen(OnsiteApp app)
        {
            this.app = app;
            AddToClassList("screen");
            AddToClassList("me-screen");
            var panel = new HudPanel("PERSONAL ARCHIVE", "自分の記録", app.ShowMap);
            Add(panel);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("screen-scroll");
            panel.Body.Add(scroll);
            var profile = new VisualElement(); profile.AddToClassList("archive-profile");
            profile.Add(UiKit.Author(app.Nickname, "PERSONAL FIELD ARCHIVE"));
            profile.Add(UiKit.Label(app.Nickname, "heading archive-name"));
            scroll.Add(profile);
            scroll.Add(UiKit.Label("YOUR FIGURE / あなたの像", "section-label"));
            var stage = new VisualElement { name = "avatar-stage", pickingMode = PickingMode.Ignore };
            stage.AddToClassList("avatar-stage");
            scroll.Add(stage);
            scroll.Add(new AvatarPicker(app.Look, app.SetLook));
            stats = new VisualElement();
            stats.AddToClassList("stat-grid");
            scroll.Add(stats);
            scroll.Add(UiKit.Rule());
            scroll.Add(UiKit.Label("TODAY'S LOG / 今日のおでかけ", "section-label"));
            distance = UiKit.Label("", "mono archive-distance");
            scroll.Add(distance);
            log = new VisualElement();
            scroll.Add(log);
            scroll.Add(UiKit.Rule());
            scroll.Add(UiKit.Label("FIELD SETTINGS / 設定", "section-label"));
            var feedback = UiKit.Row(); feedback.AddToClassList("feedback-settings");
            sound = new Toggle("サウンド");
            sound.SetValueWithoutNotify(!Sound.Muted);
            UiKit.PressFeedback(sound);
            sound.RegisterValueChangedCallback(e =>
            {
                Sound.UserGesture();
                Sound.Muted = !e.newValue;
                Sound.Tap();
                DeviceBridge.Feedback(HapticKind.Tap);
            });
            haptics = new Toggle("振動");
            haptics.SetValueWithoutNotify(DeviceBridge.HapticsEnabled);
            UiKit.PressFeedback(haptics);
            haptics.RegisterValueChangedCallback(e =>
            {
                Sound.UserGesture();
                DeviceBridge.HapticsEnabled = e.newValue;
                Sound.Tap();
                DeviceBridge.Feedback(HapticKind.Tap);
            });
            feedback.Add(sound); feedback.Add(haptics); scroll.Add(feedback);
            demo = new Toggle("デモ：地図をタップして移動");
            UiKit.PressFeedback(demo);
            demo.RegisterValueChangedCallback(e => UiKit.Activate(() => app.SetDemo(e.newValue)));
            scroll.Add(demo);
            scroll.Add(UiKit.Label("デモ中は、地図の何もない場所をタップすると現在地が移ります。", "caption"));
            sharing = new Toggle("みんなの踏み跡に加える");
            UiKit.PressFeedback(sharing);
            sharing.RegisterValueChangedCallback(e => UiKit.Activate(() => app.SetShareFootprints(e.newValue)));
            scroll.Add(sharing);
            scroll.Add(UiKit.Label("約33m四方のマスごとの回数だけを共有します。自宅の周りなどではオフのままにしてください。自分の足あとは端末内に保存されます。", "caption"));
            var reset = UiKit.Button("この端末の設定と足あとをリセット", app.ResetLocal);
            reset.AddToClassList("danger");
            scroll.Add(reset);
            scroll.Add(UiKit.Label("リセット前に確認画面が開きます。サーバーの投稿は削除されません。", "caption"));
            Refresh();
        }

        public void Refresh()
        {
            demo.SetValueWithoutNotify(app.DemoMode);
            sharing.SetValueWithoutNotify(app.ShareFootprints);
            sound.SetValueWithoutNotify(!Sound.Muted);
            haptics.SetValueWithoutNotify(DeviceBridge.HapticsEnabled);
            stats.Clear();
            AddStat("OPENED / ひらいた", app.Profile?.opened ?? app.Api?.Opened.Count ?? 0);
            AddStat("PLACED / 置いた", app.Profile?.placed ?? app.Api?.Placed.Count ?? 0);
            AddStat("VISITORS / ひらかれた", app.Profile?.opensReceived ?? 0);
            AddStat("LIKES / もらった", app.Profile?.likesReceived ?? 0);
            distance.text = (app.Trail?.Day ?? "") + " / " + AppFormat.Distance(app.Trail?.Distance ?? 0);
            log.Clear();
            if (app.Api == null) return;
            var entries = app.Api.Opened.Where(o => AppFormat.Today(o.openedAt))
                .Select(o => new LogRow { Id = o.id, Text = o.teaser, Kind = "OPEN", At = o.openedAt })
                .Concat(app.Api.Placed.Where(p => AppFormat.Today(p.createdAt))
                .Select(p => new LogRow { Id = p.id, Text = p.teaser, Kind = "PLACE", At = p.createdAt }))
                .OrderByDescending(x => x.At ?? DateTime.MinValue).ToList();
            if (entries.Count == 0)
            {
                log.Add(UiKit.Label("まだ今日の記録はありません。地図の予告に近づいてみよう。", "empty"));
                return;
            }
            foreach (var entry in entries)
            {
                var item = entry;
                var row = UiKit.Button("", () =>
                {
                    var drop = app.Api.Drops.FirstOrDefault(d => d.id == item.Id);
                    if (drop != null) app.SelectDrop(drop);
                    else app.Toast("この投稿は現在の一覧にありません。");
                });
                row.AddToClassList("archive-entry");
                row.Add(UiKit.Label(item.At?.ToLocalTime().ToString("HH:mm") ?? "--:--", "mono timeline-time"));
                var copy = new VisualElement(); copy.AddToClassList("timeline-copy");
                copy.Add(UiKit.Label(item.Kind, "section-label"));
                copy.Add(UiKit.Label(item.Text, "body-copy")); row.Add(copy); log.Add(row);
            }
        }

        private void AddStat(string title, long value)
        {
            var stat = new VisualElement();
            stat.AddToClassList("stat");
            stat.Add(UiKit.Label(value.ToString(), "stat-value mono"));
            stat.Add(UiKit.Label(title, "stat-label"));
            stats.Add(stat);
        }

        private sealed class LogRow
        {
            public string Id, Text, Kind;
            public DateTime? At;
        }
    }
}



