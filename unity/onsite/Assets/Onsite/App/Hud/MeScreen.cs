using System;
using System.Linq;
using Onsite.App.Ui;
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
            scroll.Add(UiKit.Label(app.Nickname, "heading"));
            scroll.Add(UiKit.Label("YOUR FIGURE / あなたの像", "section-label"));
            var stage = new VisualElement { name = "avatar-stage", pickingMode = PickingMode.Ignore };
            stage.AddToClassList("avatar-stage");
            scroll.Add(stage);
            scroll.Add(new AvatarPicker(app.Look, app.SetLook));
            stats = new VisualElement();
            stats.AddToClassList("stat-grid");
            scroll.Add(stats);
            scroll.Add(UiKit.Label("TODAY'S LOG / 今日のおでかけ", "section-label"));
            distance = UiKit.Label("", "mono");
            scroll.Add(distance);
            log = new VisualElement();
            scroll.Add(log);
            scroll.Add(UiKit.Label("FIELD SETTINGS / 設定", "section-label"));
            demo = new Toggle("デモ：地図をタップして移動");
            demo.RegisterValueChangedCallback(e => app.SetDemo(e.newValue));
            scroll.Add(demo);
            scroll.Add(UiKit.Label("デモ中は、地図の何もない場所をタップすると現在地が移ります。", "caption"));
            sharing = new Toggle("みんなの踏み跡に加える");
            sharing.RegisterValueChangedCallback(e => app.SetShareFootprints(e.newValue));
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
            var table = new DataTable("TIME", "SIGNAL", "TYPE");
            foreach (var entry in entries)
                table.AddRow(new[] { entry.At?.ToLocalTime().ToString("HH:mm") ?? "--:--", entry.Text, entry.Kind }, () =>
                {
                    var drop = app.Api.Drops.FirstOrDefault(d => d.id == entry.Id);
                    if (drop != null) app.SelectDrop(drop);
                    else app.Toast("この投稿は現在の一覧にありません。");
                });
            log.Add(table);
        }

        private void AddStat(string title, long value)
        {
            var stat = new VisualElement();
            stat.AddToClassList("stat");
            stat.Add(UiKit.Label(value.ToString(), "stat-value"));
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



