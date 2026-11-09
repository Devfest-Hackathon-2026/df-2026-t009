using System;
using System.Collections.Generic;
using System.Linq;
using Onsite.App.Ui;
using Onsite.Map;
using Onsite.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Hud
{
    public sealed class MapHud : VisualElement
    {
        sealed class SignalRow
        {
            public Button Root;
            public Label Title, Meta, Distance;
            public HexIcon Icon;
        }
        readonly OnsiteApp app;
        readonly Label coordinates, gps, level, xpCaption, xpNumber, distance, statusCoordinates;
        readonly Label demo, openedCount, placedCount, signalCount, signalEmpty;
        readonly Label selectedAuthor, selectedTeaser, selectedDistance, selectedRadius, routeTitle, routeCount;
        readonly SegmentBar xp, openedGauge, placedGauge, approach, routeGauge;
        readonly MetricChip openMetric, likeMetric, commentMetric;
        readonly Button openAction;
        readonly HexIcon selectedIcon;
        readonly IconButton notice;
        readonly HudPanel signals, selected, status, placement, route;
        readonly Crosshair crosshair;
        readonly ScrollView signalList;
        readonly Dictionary<string, IconButton> navigation = new Dictionary<string, IconButton>();
        readonly Dictionary<string, SignalRow> signalRows = new Dictionary<string, SignalRow>();
        bool mapVisible = true, placing, selectedVisible;
        string signalOrder = "", selectedId;
        IVisualElementScheduledItem logoHold;

        public MapHud(OnsiteApp app)
        {
            this.app = app;
            pickingMode = PickingMode.Ignore;
            AddToClassList("map-hud");
            var header = new VisualElement { pickingMode = PickingMode.Ignore }; header.AddToClassList("hud-header");
            var brand = new VisualElement { pickingMode = PickingMode.Ignore }; brand.AddToClassList("header-brand");
            brand.Add(UiKit.Label("YOUR FIELD / YOUR STORIES", "header-eyebrow mono"));
            var wordmark = UiKit.Label("ONSITE", "wordmark");
            wordmark.RegisterCallback<PointerDownEvent>(_ =>
            {
                logoHold?.Pause();
                logoHold = wordmark.schedule.Execute(app.ShowDebug); logoHold.StartingIn(850);
            });
            wordmark.RegisterCallback<PointerUpEvent>(_ => logoHold?.Pause());
            wordmark.RegisterCallback<PointerLeaveEvent>(_ => logoHold?.Pause());
            var title = UiKit.Row(wordmark, UiKit.Label("街に、ひらく。", "header-japanese")); title.AddToClassList("header-title-row");
            brand.Add(title);
            var headerBand = new LaserBand { Scan = true }; headerBand.AddToClassList("header-band"); brand.Add(headerBand);
            coordinates = UiKit.Label("FIELD / ACQUIRING", "field-coordinates mono"); brand.Add(coordinates);
            gps = UiKit.Label("GPS / WAITING FOR FIX", "gps-label mono");
            demo = UiKit.Label("DEMO", "demo-chip mono"); brand.Add(UiKit.Row(gps, demo)); header.Add(brand);
            var levelBlock = new VisualElement { pickingMode = PickingMode.Ignore }; levelBlock.AddToClassList("hud-level");
            level = UiKit.Label("LV. 1", "level mono"); levelBlock.Add(level);
            xpNumber = UiKit.Label("0 / 50", "xp-number mono"); levelBlock.Add(xpNumber);
            xp = new SegmentBar(20); levelBlock.Add(xp);
            xpCaption = UiKit.Label("EXPERIENCE / NEXT +50", "xp-caption mono"); levelBlock.Add(xpCaption);
            distance = UiKit.Label("TRAIL / 0 m", "trail-distance mono"); levelBlock.Add(distance);
            header.Add(levelBlock); Add(header);

            var rail = new VisualElement(); rail.AddToClassList("hud-rail");
            void Nav(string view, string icon, string label, Action action)
            {
                var button = new IconButton(icon, label, action); navigation[view] = button; rail.Add(button);
            }
            Nav("map", "map", "FIELD", app.ShowMap);
            Nav("feed", "feed", "JOURNAL", app.ShowFeed);
            Nav("post", "plus", "LEAVE", app.BeginPost);
            Nav("events", "route", "ROUTES", app.ShowEvents);
            notice = new IconButton("notice", "SIGNALS", app.ShowNotices); navigation["notices"] = notice; rail.Add(notice);
            Nav("me", "me", "MY FIELD", app.ShowMe); Add(rail); SetActiveView("map");

            signals = new HudPanel("NEARBY / SIGNALS", "近くの予告"); signals.AddToClassList("field-signals");
            signalCount = UiKit.Label("00 SIGNALS", "signal-count mono"); signals.Body.Add(signalCount);
            signalEmpty = UiKit.Label("まだ予告がありません。最初の発見を置こう。", "empty"); signals.Body.Add(signalEmpty);
            signalList = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            signalList.AddToClassList("signal-list"); signals.Body.Add(signalList); Add(signals);

            selected = new HudPanel("SIGNAL / INFORMATION", "予告", app.ClearSelection); selected.AddToClassList("selected-drop");
            selectedAuthor = UiKit.Label("", "selected-identity muted"); selected.Body.Add(selectedAuthor);
            selectedIcon = new HexIcon(); selectedTeaser = UiKit.Label("", "selected-teaser");
            var teaserRow = UiKit.Row(selectedIcon, selectedTeaser); teaserRow.AddToClassList("selected-title-row"); selected.Body.Add(teaserRow);
            openMetric = new MetricChip("footprint", "0"); likeMetric = new MetricChip("heart", "0"); commentMetric = new MetricChip("comment", "0");
            var metrics = UiKit.Row(openMetric, likeMetric, commentMetric); metrics.AddToClassList("selected-metrics"); selected.Body.Add(metrics);
            selectedDistance = UiKit.Label("— m", "selected-distance mono"); selectedRadius = UiKit.Label("", "approach-caption mono");
            var approachRow = UiKit.Row(selectedDistance, selectedRadius); approachRow.AddToClassList("selected-approach"); selected.Body.Add(approachRow);
            approach = new SegmentBar(30); selected.Body.Add(approach);
            openAction = UiKit.Button("OPEN  ひらく", app.OpenSelected, true); openAction.AddToClassList("primary-action"); selected.Body.Add(openAction);
            var hints = UiKit.Row(UiKit.Label("○  地図で選ぶ", "button-hint"), UiKit.Button("○  閉じる", app.ClearSelection));
            hints.AddToClassList("action-hints"); selected.Body.Add(hints); Add(selected);

            status = new HudPanel("FIELD / JOURNAL", "今日のフィールド"); status.AddToClassList("hud-status");
            openedGauge = new SegmentBar(12); placedGauge = new SegmentBar(12) { Tint = MarkerField.Amber };
            var opened = new VisualElement(); opened.AddToClassList("status-metric"); opened.Add(UiKit.Label("OPENED / TODAY", "section-label"));
            openedCount = UiKit.Label("00", "mono status-count"); opened.Add(openedCount); opened.Add(openedGauge);
            var placed = new VisualElement(); placed.AddToClassList("status-metric"); placed.Add(UiKit.Label("PLACED / TODAY", "section-label"));
            placedCount = UiKit.Label("00", "mono status-count"); placed.Add(placedCount); placed.Add(placedGauge);
            statusCoordinates = UiKit.Label("NO FIX", "mono status-coordinates");
            var statusRow = UiKit.Row(opened, placed, statusCoordinates); statusRow.AddToClassList("status-metrics"); status.Body.Add(statusRow); Add(status);

            placement = new HudPanel("LEAVE / A TRACE", "地図を動かして場所を選ぶ"); placement.AddToClassList("placement-bar");
            placement.Body.Add(UiKit.Label("照準の地面に投稿を置きます。", "muted"));
            var placementActions = UiKit.Row(UiKit.Button("○  やめる", app.CancelPlacement), UiKit.Button("PLACE  この場所に置く", app.ConfirmPlacement, true));
            placementActions.AddToClassList("placement-actions"); placement.Body.Add(placementActions); Add(placement);
            crosshair = new Crosshair(); crosshair.AddToClassList("placement-crosshair"); Add(crosshair);
            route = new HudPanel("ACTIVE / ROUTE", "巡りルート"); route.AddToClassList("route-strip");
            routeTitle = UiKit.Label("", "route-title"); route.Body.Add(routeTitle);
            routeGauge = new SegmentBar(1) { Tint = MarkerField.Amber }; route.Body.Add(routeGauge);
            routeCount = UiKit.Label("", "route-count mono");
            var routeActions = UiKit.Row(routeCount, UiKit.Button("○  ルートを閉じる", app.ClearRoute)); routeActions.AddToClassList("route-actions"); route.Body.Add(routeActions); Add(route);
            SetPlacement(false);
        }

        public void SetActiveView(string view)
        {
            foreach (var pair in navigation)
            {
                bool active = pair.Key == view || (pair.Key == "post" && view == "placement");
                pair.Value.EnableInClassList("active", active); pair.Value.EnableInClassList("selected", active);
            }
        }
        public void SetMapVisible(bool visible)
        {
            mapVisible = visible;
            // 画面切替の音は Screen 側で鳴らす。
            selectedVisible = visible && !placing && app.Selected != null;
            if (!visible) SetPlacement(false); Refresh();
        }
        public void SetPlacement(bool value)
        {
            bool changed = placing != value; placing = value;
            placement.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            crosshair.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            if (changed && value) Anim.Enter(placement);
            if (value) SetActiveView("placement"); else if (mapVisible) SetActiveView("map");
            Refresh();
        }
        public void FlashSelection()
        {
            Anim.Flash(selected); selected.Q<LaserBand>()?.Flash();
        }
        public void Refresh()
        {
            coordinates.text = app.HasLocation ? "FIELD / " + AppFormat.Coordinates(app.Lat, app.Lng) : "FIELD / ACQUIRING";
            gps.text = app.HasLocation ? "GPS ± " + app.Accuracy.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " m" : "GPS / WAITING FOR FIX";
            demo.style.display = app.DemoMode ? DisplayStyle.Flex : DisplayStyle.None;
            var info = AppFormat.LevelOf(app.Profile); level.text = "LV. " + info.Level;
            xp.Value = info.Progress; xpNumber.text = info.Xp + " / " + (info.Xp + info.ToNext); xpCaption.text = "EXPERIENCE / NEXT +" + info.ToNext;
            distance.text = "TRAIL / " + AppFormat.Distance(app.Trail?.Distance ?? 0);
            EnableInClassList("has-selection", app.Selected != null); EnableInClassList("has-route", app.ActiveRoute != null);
            var unread = app.Api?.Notices.Count(n => app.IsUnread(n)) ?? 0; notice.Badge = unread > 0 ? unread.ToString() : "";
            statusCoordinates.text = app.HasLocation ? AppFormat.Coordinates(app.Lat, app.Lng) : "NO FIX";
            var opened = app.Api?.Opened.Count(o => AppFormat.Today(o.openedAt)) ?? 0;
            var placed = app.Api?.Placed.Count(o => AppFormat.Today(o.createdAt)) ?? 0;
            openedCount.text = opened.ToString("00"); placedCount.text = placed.ToString("00");
            openedGauge.Value = Mathf.Clamp01(opened / 12f); openedGauge.tooltip = "今日ひらいた数 " + opened;
            placedGauge.Value = Mathf.Clamp01(placed / 12f); placedGauge.tooltip = "今日置いた数 " + placed;
            // スマホでは投稿を選んでいる間、下の集計を隠す（シートと重なるため）
            bool phone = parent != null && parent.ClassListContains("phone");
            status.style.display = mapVisible && !placing && !(phone && app.Selected != null) ? DisplayStyle.Flex : DisplayStyle.None;
            signals.style.display = mapVisible && !placing ? new StyleEnum<DisplayStyle>(StyleKeyword.Null) : new StyleEnum<DisplayStyle>(DisplayStyle.None);
            bool showSelected = mapVisible && !placing && app.Selected != null;
            if (selectedVisible != showSelected)
            {
                Sound.Play(showSelected ? "open_panel" : "close_panel");
                selectedVisible = showSelected;
            }
            selected.style.display = showSelected ? DisplayStyle.Flex : DisplayStyle.None;
            route.style.display = mapVisible && !placing && app.ActiveRoute != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (!mapVisible || placing) return;
            RefreshSignals();
            if (app.Selected != null) RenderSelected(); else selectedId = null;
            if (app.ActiveRoute != null)
            {
                var ev = app.ActiveRoute; var done = ev.stops.Count(s => app.OpenedIds.Contains(s.id)); routeTitle.text = ev.title;
                routeGauge.Cells = Mathf.Max(1, ev.stops.Length); routeGauge.Value = ev.stops.Length == 0 ? 0 : (float)done / ev.stops.Length;
                routeCount.text = done.ToString("00") + " / " + ev.stops.Length.ToString("00") + " STOPS";
            }
        }
        void RefreshSignals()
        {
            var list = app.Api?.Drops.OrderBy(app.DistanceTo).Take(8).ToArray() ?? Array.Empty<Drop>();
            signals.EnableInClassList("has-signals", list.Length > 0);
            signalCount.text = list.Length.ToString("00") + " SIGNALS / NEAREST FIRST";
            signalEmpty.style.display = list.Length == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            signalList.style.display = list.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            var order = string.Join("\n", list.Select(d => d.id));
            // 周期更新では要素を作り直さず、フォーカスとスクロール位置を保つ。
            if (signalOrder != order)
            {
                signalOrder = order; signalList.Clear(); signalRows.Clear();
                foreach (var drop in list)
                {
                    var id = drop.id; var row = new SignalRow();
                    row.Root = UiKit.Button("", () =>
                    {
                        var found = app.Api?.Drops.FirstOrDefault(d => d.id == id); if (found != null) app.SelectDrop(found);
                    });
                    row.Root.AddToClassList("signal-row"); row.Icon = new HexIcon(); row.Icon.pickingMode = PickingMode.Ignore; row.Root.Add(row.Icon);
                    var copy = new VisualElement { pickingMode = PickingMode.Ignore }; copy.AddToClassList("signal-copy");
                    row.Title = UiKit.Label("", "signal-title"); row.Meta = UiKit.Label("", "signal-meta mono"); copy.Add(row.Title); copy.Add(row.Meta); row.Root.Add(copy);
                    row.Distance = UiKit.Label("", "signal-distance mono"); row.Root.Add(row.Distance); signalRows[id] = row; signalList.Add(row.Root);
                }
            }
            foreach (var drop in list)
            {
                var row = signalRows[drop.id]; row.Icon.State = app.StateOf(drop); row.Title.text = drop.teaser;
                row.Distance.text = AppFormat.Distance(app.DistanceTo(drop)); row.Meta.text = "R " + AppFormat.Distance(drop.radius) + "  /  ♥ " + (drop.likeCount ?? 0);
                row.Root.tooltip = drop.teaser + " / " + row.Distance.text + " / " + row.Meta.text; row.Root.EnableInClassList("selected", app.Selected?.id == drop.id);
            }
        }
        void RenderSelected()
        {
            var drop = app.Selected; var state = app.StateOf(drop); var metres = app.DistanceTo(drop); bool changed = selectedId != drop.id; selectedId = drop.id;
            selectedAuthor.text = drop.authorName + "  /  " + AppFormat.Ago(drop.createdAt); selectedIcon.State = state; selectedTeaser.text = drop.teaser;
            openMetric.Number.text = drop.openCount.ToString(); likeMetric.Number.text = (drop.likeCount ?? 0).ToString(); commentMetric.Number.text = (drop.commentCount ?? 0).ToString();
            selectedDistance.text = AppFormat.Distance(metres); selectedRadius.text = "DISTANCE / RADIUS " + AppFormat.Distance(drop.radius);
            bool canOpen = state != DropState.Locked; selected.EnableInClassList("in-range", canOpen);
            approach.Value = canOpen ? 1 : app.HasLocation ? Mathf.Clamp01(1 - (float)(metres - drop.radius) / 1000) : 0; approach.Tint = canOpen ? MarkerField.Amber : MarkerField.Cyan;
            string label = state == DropState.Mine || state == DropState.Opened ? "READ  読む" : canOpen ? "OPEN  ひらく" : app.HasLocation ? "あと " + AppFormat.Distance(Math.Max(0, metres - drop.radius - Math.Min(app.Accuracy, 30))) : "現在地を取得しています";
            openAction.text = app.Busy ? "VERIFYING  照合中…" : label; openAction.SetEnabled(canOpen && !app.Busy);
            if (changed) Anim.Enter(selected);
        }
    }
}
