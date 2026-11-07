using System;
using System.Linq;
using Onsite.App.Ui;
using Onsite.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Hud
{
    public sealed class MapHud : VisualElement
    {
        readonly OnsiteApp app;
        readonly Label coordinates, gps, level, xpCaption, distance, statusCoordinates;
        readonly Label demo, openedCount, placedCount;
        readonly SegmentBar xp, openedGauge, placedGauge;
        readonly IconButton notice;
        readonly HudPanel signals, selected, status, placement, route;
        readonly Crosshair crosshair;
        readonly VisualElement signalBody;
        bool mapVisible = true, placing;
        IVisualElementScheduledItem logoHold;

        public MapHud(OnsiteApp app)
        {
            this.app = app;
            pickingMode = PickingMode.Ignore;
            AddToClassList("map-hud");
            var header = new VisualElement { pickingMode = PickingMode.Ignore }; header.AddToClassList("hud-header");
            var brand = new VisualElement(); brand.AddToClassList("header-brand");
            var wordmark = UiKit.Label("ONSITE", "wordmark");
            wordmark.RegisterCallback<PointerDownEvent>(_ =>
            {
                logoHold?.Pause();
                logoHold = wordmark.schedule.Execute(app.ShowDebug); logoHold.StartingIn(850);
            });
            wordmark.RegisterCallback<PointerUpEvent>(_ => logoHold?.Pause());
            wordmark.RegisterCallback<PointerLeaveEvent>(_ => logoHold?.Pause());
            brand.Add(wordmark);
            coordinates = UiKit.Label("FIELD / ACQUIRING", "field-coordinates mono"); brand.Add(coordinates);
            gps = UiKit.Label("GPS / WAITING FOR FIX", "gps-label mono");
            demo = UiKit.Label("DEMO", "demo-chip mono");
            brand.Add(UiKit.Row(gps, demo)); header.Add(brand);
            var levelBlock = new VisualElement(); levelBlock.AddToClassList("hud-level");
            level = UiKit.Label("LV.1", "level mono"); levelBlock.Add(level);
            xp = new SegmentBar(20); levelBlock.Add(xp);
            xpCaption = UiKit.Label("0 EXP", "xp-caption mono"); levelBlock.Add(xpCaption);
            distance = UiKit.Label("TRAIL / 0 m", "mono"); levelBlock.Add(distance);
            header.Add(levelBlock); Add(header);

            var rail = new VisualElement(); rail.AddToClassList("hud-rail");
            rail.Add(new IconButton("map", "MAP", app.ShowMap));
            rail.Add(new IconButton("feed", "FEED", app.ShowFeed));
            rail.Add(new IconButton("plus", "POST", app.BeginPost));
            notice = new IconButton("notice", "NOTICE", app.ShowNotices); rail.Add(notice);
            rail.Add(new IconButton("me", "ME", app.ShowMe)); Add(rail);

            signals = new HudPanel("FIELD SIGNALS", "近くの予告"); signals.AddToClassList("field-signals");
            signalBody = signals.Body; Add(signals);
            selected = new HudPanel("OPEN ORDER", "予告", app.ClearSelection); selected.AddToClassList("selected-drop"); Add(selected);
            status = new HudPanel("SURVEY LOG", "今日のフィールド"); status.AddToClassList("hud-status");
            openedGauge = new SegmentBar(12); placedGauge = new SegmentBar(12) { Tint = MarkerField.Amber };
            var opened = new VisualElement(); opened.Add(UiKit.Label("OPENED / TODAY", "section-label")); opened.Add(openedGauge); openedCount = UiKit.Label("0", "mono status-count"); opened.Add(openedCount);
            var placed = new VisualElement(); placed.Add(UiKit.Label("PLACED / TODAY", "section-label")); placed.Add(placedGauge); placedCount = UiKit.Label("0", "mono status-count"); placed.Add(placedCount);
            statusCoordinates = UiKit.Label("NO FIX", "mono status-coordinates");
            status.Body.Add(UiKit.Row(opened, placed, statusCoordinates)); Add(status);

            placement = new HudPanel("PLACE SIGNAL", "地図を動かして場所を選ぶ"); placement.AddToClassList("placement-bar");
            placement.Body.Add(UiKit.Label("照準の地面に投稿を置きます。", "muted"));
            placement.Body.Add(UiKit.Row(UiKit.Button("やめる", app.CancelPlacement), UiKit.Button("この場所に置く", app.ConfirmPlacement, true))); Add(placement);
            crosshair = new Crosshair(); crosshair.AddToClassList("placement-crosshair"); Add(crosshair);
            route = new HudPanel("ROUTE", "巡りルート"); route.AddToClassList("route-strip"); Add(route);
            SetPlacement(false);
        }

        public void SetMapVisible(bool visible)
        {
            mapVisible = visible;
            if (!visible) SetPlacement(false);
            Refresh();
        }
        public void SetPlacement(bool value)
        {
            placing = value;
            placement.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            crosshair.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            Refresh();
        }
        public void FlashSelection()
        {
            selected.AddToClassList("selection-flash");
            selected.schedule.Execute(() => selected.RemoveFromClassList("selection-flash")).StartingIn(220);
        }
        public void Refresh()
        {
            coordinates.text = app.HasLocation ? "FIELD / " + AppFormat.Coordinates(app.Lat, app.Lng) : "FIELD / ACQUIRING";
            gps.text = app.HasLocation ? "GPS ± " + app.Accuracy.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " m" : "GPS / WAITING FOR FIX";
            demo.style.display = app.DemoMode ? DisplayStyle.Flex : DisplayStyle.None;
            var info = AppFormat.LevelOf(app.Profile);
            level.text = "LV." + info.Level;
            xp.Value = info.Progress;
            xpCaption.text = info.Xp + " EXP / NEXT " + info.ToNext;
            distance.text = "TRAIL / " + AppFormat.Distance(app.Trail?.Distance ?? 0);
            EnableInClassList("has-selection", app.Selected != null);
            EnableInClassList("has-route", app.ActiveRoute != null);
            var unread = app.Api?.Notices.Count(n => app.IsUnread(n)) ?? 0;
            notice.Badge = unread > 0 ? unread.ToString() : "";
            statusCoordinates.text = app.HasLocation ? AppFormat.Coordinates(app.Lat, app.Lng) : "NO FIX";
            var opened = app.Api?.Opened.Count(o => AppFormat.Today(o.openedAt)) ?? 0;
            var placed = app.Api?.Placed.Count(o => AppFormat.Today(o.createdAt)) ?? 0;
            openedCount.text = opened.ToString("00"); placedCount.text = placed.ToString("00");
            openedGauge.Value = Mathf.Clamp01(opened / 12f); openedGauge.tooltip = "今日ひらいた数 " + opened;
            placedGauge.Value = Mathf.Clamp01(placed / 12f); placedGauge.tooltip = "今日置いた数 " + placed;
            status.style.display = mapVisible && !placing ? DisplayStyle.Flex : DisplayStyle.None;
            signals.style.display = mapVisible && !placing ? DisplayStyle.Flex : DisplayStyle.None;
            selected.style.display = mapVisible && !placing && app.Selected != null ? DisplayStyle.Flex : DisplayStyle.None;
            route.style.display = mapVisible && !placing && app.ActiveRoute != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (!mapVisible || placing) return;
            signalBody.Clear();
            var table = new DataTable("NO", "NAME", "DIST", "RADIUS", "♥");
            var list = app.Api?.Drops.OrderBy(app.DistanceTo).Take(8).ToArray();
            if (list == null || list.Length == 0) signalBody.Add(UiKit.Label("まだ予告がありません。最初の発見を置こう。", "empty"));
            else
            {
                int n = 0;
                foreach (var drop in list)
                    table.AddRow(new[] { (++n).ToString("00"), drop.teaser, AppFormat.Distance(app.DistanceTo(drop)), AppFormat.Distance(drop.radius), (drop.likeCount ?? 0).ToString() }, () => app.SelectDrop(drop), app.Selected?.id == drop.id);
                signalBody.Add(table);
            }
            if (app.Selected != null) RenderSelected();
            if (app.ActiveRoute != null)
            {
                route.Body.Clear();
                var ev = app.ActiveRoute;
                var done = ev.stops.Count(s => app.OpenedIds.Contains(s.id));
                route.Body.Add(UiKit.Label(ev.title, "route-title"));
                route.Body.Add(new SegmentBar(ev.stops.Length) { Value = ev.stops.Length == 0 ? 0 : (float)done / ev.stops.Length, Tint = MarkerField.Amber });
                route.Body.Add(UiKit.Row(UiKit.Label(done + " / " + ev.stops.Length, "mono"), UiKit.Button("ルートを閉じる", app.ClearRoute)));
            }
        }
        void RenderSelected()
        {
            var drop = app.Selected;
            var state = app.StateOf(drop);
            var metres = app.DistanceTo(drop);
            selected.Body.Clear();
            selected.Body.Add(UiKit.Label(drop.authorName + "  /  " + AppFormat.Ago(drop.createdAt), "muted"));
            selected.Body.Add(UiKit.Row(new HexIcon(state), UiKit.Label(drop.teaser, "selected-teaser")));
            selected.Body.Add(UiKit.Row(new MetricChip("footprint", drop.openCount.ToString()), new MetricChip("heart", (drop.likeCount ?? 0).ToString()), new MetricChip("comment", (drop.commentCount ?? 0).ToString())));
            selected.Body.Add(UiKit.Label("DIST " + AppFormat.Distance(metres) + "   /   RADIUS " + AppFormat.Distance(drop.radius), "mono approach-caption"));
            bool canOpen = state != DropState.Locked;
            selected.Body.Add(new SegmentBar(30) { Value = canOpen ? 1 : app.HasLocation ? Mathf.Clamp01(1 - (float)(metres - drop.radius) / 1000) : 0, Tint = canOpen ? MarkerField.Amber : MarkerField.Cyan });
            string label = state == DropState.Mine || state == DropState.Opened ? "読む" : canOpen ? "ひらく" : app.HasLocation ? "あと " + AppFormat.Distance(Math.Max(0, metres - drop.radius - Math.Min(app.Accuracy, 30))) : "現在地を取得しています";
            var button = UiKit.Button(app.Busy ? "照合中…" : label, app.OpenSelected, true);
            button.AddToClassList("primary-action");
            button.SetEnabled(canOpen && !app.Busy);
            selected.Body.Add(button);
        }
    }
}


