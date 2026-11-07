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
            var panel = new HudPanel("FIELD FEED", "まわりの予告", app.ShowMap);
            Add(panel);
            near = UiKit.Button("近い順", () => { byNewest = false; Refresh(); });
            recent = UiKit.Button("新着順", () => { byNewest = true; Refresh(); });
            panel.Body.Add(UiKit.Row(near, recent, UiKit.Button("イベント", app.ShowEvents)));
            panel.Body.Add(UiKit.Label("本文と写真は、近づいた人だけ。", "caption"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("screen-scroll");
            panel.Body.Add(scroll);
            list = scroll.contentContainer;
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
                var icon = new HexIcon(app.StateOf(drop));
                var head = UiKit.Row(icon, UiKit.Label("SIGNAL / " + (++number).ToString("00"), "section-label"),
                    UiKit.Label(app.HasLocation ? AppFormat.Distance(app.DistanceTo(drop)) : "GPS待機", "mono"));
                card.Add(head);
                card.Add(UiKit.Label(drop.teaser, "heading"));
                card.Add(UiKit.Label((drop.authorName ?? "旅人") + " / " + AppFormat.Ago(drop.createdAt), "muted"));
                card.Add(UiKit.Row(new MetricChip("foot", drop.openCount.ToString()),
                    new MetricChip("heart", (drop.likeCount ?? 0).ToString()),
                    new MetricChip("comment", (drop.commentCount ?? 0).ToString())));
                if (drop.hasPhoto) card.Add(UiKit.Label("PHOTO / 写真は現地で", "caption"));
                card.Add(UiKit.Button("地図で見る", () => app.SelectDrop(drop), true));
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


