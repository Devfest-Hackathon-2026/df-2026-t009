using System;
using System.Linq;
using Onsite.App.Ui;
using UnityEngine.UIElements;

namespace Onsite.App.Hud
{
    public sealed class FeedScreen : VisualElement
    {
        private readonly OnsiteApp app;
        private readonly VisualElement list;
        private readonly Button near;
        private readonly Button recent;
        private bool byNewest;
        public FeedScreen(OnsiteApp app)
        {
            this.app = app;
            AddToClassList("screen");
            AddToClassList("feed-screen");
            var panel = new HudPanel("FIELD FEED", "まわりの予告", app.ShowMap);
            Add(panel);
            near = UiKit.Button("近い順", () => { byNewest = false; Refresh(); });
            recent = UiKit.Button("新着順", () => { byNewest = true; Refresh(); });
            var toolbar = UiKit.Row(near, recent, UiKit.Button("ROUTES / イベント", app.ShowEvents));
            toolbar.AddToClassList("content-toolbar"); panel.Body.Add(toolbar);
            panel.Body.Add(UiKit.Label("本文と写真は、近づいた人だけ。", "caption"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("screen-scroll");
            panel.Body.Add(scroll);
            list = scroll.contentContainer;
            list.AddToClassList("feed-list");
            Refresh();
        }

        public void Refresh()
        {
            near.EnableInClassList("on", !byNewest);
            recent.EnableInClassList("on", byNewest);
            list.Clear();
            if (app.Api == null || app.Api.Drops.Count == 0)
            {
                list.Add(UiKit.Label("まだ予告はありません。地図の＋から最初の発見を置いてみよう。", "empty"));
                return;
            }
            var sorted = byNewest
                ? app.Api.Drops.OrderByDescending(d => d.createdAt ?? DateTime.MinValue)
                : app.Api.Drops.OrderBy(app.DistanceTo);
            var number = 0;
            foreach (var drop in sorted)
            {
                var card = new VisualElement { focusable = true };
                card.AddToClassList("card");
                card.AddToClassList("feed-card");
                card.Add(UiKit.Author(drop.authorName ?? "旅人", AppFormat.Ago(drop.createdAt)));
                var head = UiKit.Row(new HexIcon(app.StateOf(drop)), UiKit.Label("SIGNAL / " + (++number).ToString("00"), "section-label"),
                    UiKit.Label(app.HasLocation ? AppFormat.Distance(app.DistanceTo(drop)) : "GPS WAIT", "mono signal-distance"));
                head.AddToClassList("signal-meta"); card.Add(head);
                if (drop.hasPhoto)
                {
                    // 写真は現地でひらくまで取得しない。
                    var gate = new VisualElement(); gate.AddToClassList("photo-gate");
                    var caption = new VisualElement(); caption.AddToClassList("photo-gate-copy");
                    caption.Add(UiKit.Label("A VIEW AWAITS", "photo-gate-label"));
                    caption.Add(UiKit.Label("この場所で、写真がひらく", "caption"));
                    gate.Add(caption); card.Add(gate);
                }
                card.Add(UiKit.Label(drop.teaser, "heading feed-teaser"));
                var metrics = UiKit.Row(new MetricChip("foot", drop.openCount.ToString()),
                    new MetricChip("heart", (drop.likeCount ?? 0).ToString()),
                    new MetricChip("comment", (drop.commentCount ?? 0).ToString()));
                metrics.AddToClassList("inline-metrics");
                var view = UiKit.Button("VIEW / 地図で見る", () => app.SelectDrop(drop)); view.AddToClassList("hint-action");
                var footer = UiKit.Row(metrics, view); footer.AddToClassList("feed-footer"); card.Add(footer);
                card.Add(UiKit.Rule());
                card.RegisterCallback<ClickEvent>(e =>
                {
                    var element = e.target as VisualElement;
                    while (element != null && element != card)
                    {
                        if (element is Button) return;
                        element = element.parent;
                    }
                    app.SelectDrop(drop);
                });
                card.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == UnityEngine.KeyCode.Return) { app.SelectDrop(drop); e.StopPropagation(); }
                });
                list.Add(card);
            }
        }
    }
}


