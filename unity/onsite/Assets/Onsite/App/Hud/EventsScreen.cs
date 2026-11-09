using System;
using System.Collections.Generic;
using System.Linq;
using Onsite.App.Ui;
using Onsite.Map;
using Onsite.Net;
using UnityEngine.UIElements;

namespace Onsite.App.Hud
{
    public sealed class EventsScreen : VisualElement
    {
        private readonly OnsiteApp app;
        private readonly HudPanel frame;
        private readonly ScrollView scroll;
        private readonly List<string> picked = new List<string>();
        private string selectedId;
        private bool creating;
        private bool busy;
        private int periodDays;
        private TextField title;
        private TextField description;
        private VisualElement ordered;
        private VisualElement candidates;
        private Button submit;
        public EventsScreen(OnsiteApp app)
        {
            this.app = app;
            AddToClassList("screen");
            AddToClassList("events-screen");
            frame = new HudPanel("OPEN ORDER", "巡るイベント", app.ShowMap);
            Add(frame);
            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("screen-scroll");
            frame.Body.Add(scroll);
            Refresh();
        }

        public void Refresh()
        {
            // 通信の更新で、作成中の入力と選択順を消さない。
            if (creating) { RefreshPicker(); return; }
            scroll.Clear();
            var ev = app.Events.FirstOrDefault(e => e.id == selectedId);
            if (ev != null) DrawDetail(ev);
            else { selectedId = null; DrawList(); }
        }

        internal void ShowDetail(KokoEvent ev)
        {
            if (ev == null) return;
            creating = false; selectedId = ev.id; Refresh();
            scroll.scrollOffset = UnityEngine.Vector2.zero;
        }

        private void DrawList()
        {
            scroll.Add(UiKit.Row(UiKit.Button("イベントを作る", BeginCreate, true),
                UiKit.Button("更新", app.RefreshEvents), UiKit.Button("フィード", app.ShowFeed)));
            scroll.Add(UiKit.Label("予告をつなげて、次の一歩の順番に。中身はそれぞれの場所でひらきます。", "body-copy"));
            if (app.Events.Count == 0)
            {
                scroll.Add(UiKit.Label("まだイベントはありません。2か所以上の投稿をつないで作れます。", "empty"));
                return;
            }
            foreach (var ev in app.Events.OrderBy(e => app.JoinedIds.Contains(e.id) ? 0 : StatusRank(e)))
            {
                var stops = ev.stops ?? Array.Empty<EventStop>();
                var count = stops.Count(s => app.OpenedIds.Contains(s.id));
                var card = new VisualElement();
                card.AddToClassList("card"); card.AddToClassList("event-card");
                var status = AppFormat.EventStatus(ev).ToLowerInvariant();
                var badge = UiKit.Label(StatusLabel(status) + (app.JoinedIds.Contains(ev.id) ? " / JOINED" : ""), "section-label");
                card.Add(badge);
                card.Add(UiKit.Label(ev.title, "heading"));
                card.Add(UiKit.Label(Period(ev), "mono"));
                card.Add(UiKit.Label(stops.Length + " STOPS / " + ev.participantCount + " PEOPLE", "mono"));
                var progress = new SegmentBar { Value = stops.Length > 0 ? (float)count / stops.Length : 0, Tint = MarkerField.Amber };
                card.Add(progress);
                card.Add(UiKit.Label(count + " / " + stops.Length + " OPENED", "mono"));
                card.Add(UiKit.Button("EXPLORE / 巡る順を見る", () => { selectedId = ev.id; Refresh(); }));
                card.Add(UiKit.Rule());
                scroll.Add(card);
            }
        }

        private void DrawDetail(KokoEvent ev)
        {
            var stops = ev.stops ?? Array.Empty<EventStop>();
            var done = stops.Count(s => app.OpenedIds.Contains(s.id));
            var status = AppFormat.EventStatus(ev).ToLowerInvariant();
            scroll.Add(UiKit.Button("← 一覧へ", () => { selectedId = null; Refresh(); }));
            scroll.Add(UiKit.Label(StatusLabel(status), "section-label"));
            scroll.Add(UiKit.Label(ev.title, "heading"));
            scroll.Add(UiKit.Author(ev.authorName, AppFormat.Ago(ev.createdAt)));
            scroll.Add(UiKit.Label(Period(ev), "mono"));
            scroll.Add(UiKit.Label(ev.description ?? "", "body-copy"));
            scroll.Add(UiKit.Label(stops.Length + " STOPS / " + AppFormat.Distance(AppFormat.RouteLength(ev)) + " / " + ev.participantCount + " PEOPLE", "mono"));
            scroll.Add(new SegmentBar { Value = stops.Length > 0 ? (float)done / stops.Length : 0, Tint = MarkerField.Amber });
            scroll.Add(UiKit.Label(done + " / " + stops.Length + " OPENED", "mono"));
            var joined = app.JoinedIds.Contains(ev.id);
            var join = UiKit.Button(joined ? "参加をやめる" : "参加する", () =>
            {
                if (busy) return;
                busy = true;
                Refresh();
                app.JoinEvent(ev, !joined, success => { busy = false; Refresh(); });
            }, true);
            join.SetEnabled(!busy && (joined || status != "ended"));
            scroll.Add(UiKit.Row(UiKit.Button("ルートを表示", () => app.ShowRoute(ev)), join));
            scroll.Add(UiKit.Rule());
            scroll.Add(UiKit.Label("OPEN ORDER / 巡る順", "section-label"));
            var table = new DataTable("NO", "NAME", "DIST", "RADIUS");
            for (var i = 0; i < stops.Length; i++)
            {
                var stop = stops[i];
                var opened = app.OpenedIds.Contains(stop.id);
                var distance = app.HasLocation ? AppFormat.Distance(GeoMath.Distance(app.Lat, app.Lng, stop.lat, stop.lng)) : "—";
                table.AddRow(new[] { opened ? "✓" : (i + 1).ToString("00"), stop.teaser, distance, AppFormat.Distance(stop.radius) }, () =>
                {
                    var drop = app.Api?.Drops.FirstOrDefault(d => d.id == stop.id);
                    if (drop != null) app.SelectDrop(drop);
                    else app.Toast("この投稿は削除されたか、現在の一覧にありません。");
                }, opened);
            }
            scroll.Add(table);
        }

        private void BeginCreate()
        {
            creating = true;
            selectedId = null;
            busy = false;
            picked.Clear();
            scroll.Clear();
            scroll.Add(UiKit.Button("← やめる", () => { creating = false; Refresh(); }));
            scroll.Add(UiKit.Label("BUILD A ROUTE", "heading"));
            scroll.Add(UiKit.Label("投稿を巡る順に選んでください。上下ボタンで順番を調整できます。", "body-copy"));
            title = UiKit.Field("イベント名", 40);
            description = UiKit.Field("説明（任意）", 300, true);
            scroll.Add(title);
            scroll.Add(description);
            // 開催期間：祭りやツアーなど「この期間だけ」の巡りを作れる
            scroll.Add(UiKit.Label("PERIOD / 開催期間", "section-label"));
            var periods = new VisualElement(); periods.AddToClassList("segmented");
            periodDays = 0;
            foreach (var (days, label) in new[] { (0, "いつでも"), (1, "今日"), (3, "3日間"), (7, "1週間") })
            {
                Button b = null;
                b = UiKit.Button(label, () =>
                {
                    periodDays = days;
                    foreach (var c in periods.Children()) c.EnableInClassList("selected", c == b);
                });
                b.EnableInClassList("selected", days == 0);
                periods.Add(b);
            }
            scroll.Add(periods);
            ordered = new VisualElement();
            scroll.Add(ordered);
            submit = UiKit.Button("この順でイベントを作る", () =>
            {
                if (busy || string.IsNullOrWhiteSpace(title.value) || picked.Count < 2) return;
                busy = true;
                RefreshPicker();
                DateTime? start = null, end = null;
                if (periodDays > 0) { start = DateTime.Now; end = DateTime.Today.AddDays(periodDays).AddSeconds(-1); }
                app.CreateEvent(title.value.Trim(), description.value.Trim(), picked.ToArray(), start, end, success =>
                {
                    busy = false;
                    if (success) creating = false;
                    Refresh();
                });
            }, true);
            scroll.Add(submit);
            title.RegisterValueChangedCallback(_ => UpdateSubmit());
            scroll.Add(UiKit.Label("SIGNALS / 予告を追加", "section-label"));
            candidates = new VisualElement();
            scroll.Add(candidates);
            RefreshPicker();
        }

        private void RefreshPicker()
        {
            if (ordered == null || candidates == null) return;
            ordered.Clear();
            ordered.Add(UiKit.Label("OPEN ORDER / " + picked.Count + " / 20", "section-label"));
            for (var i = 0; i < picked.Count; i++)
            {
                var index = i;
                var id = picked[i];
                var drop = app.Api?.Drops.FirstOrDefault(d => d.id == id);
                var card = new VisualElement();
                card.AddToClassList("card"); card.AddToClassList("event-card");
                card.Add(UiKit.Label((i + 1).ToString("00") + " / " + (drop?.teaser ?? "一覧にない投稿"), "body-copy"));
                var up = UiKit.Button("↑", () => Move(index, -1));
                var down = UiKit.Button("↓", () => Move(index, 1));
                up.SetEnabled(i > 0 && !busy);
                down.SetEnabled(i < picked.Count - 1 && !busy);
                var remove = UiKit.Button("外す", () => { picked.Remove(id); RefreshPicker(); });
                remove.SetEnabled(!busy);
                card.Add(UiKit.Row(up, down, remove));
                ordered.Add(card);
            }
            if (picked.Count == 0) ordered.Add(UiKit.Label("2〜20か所を選びます。", "empty"));
            candidates.Clear();
            if (app.Api == null || app.Api.Drops.Count < 2)
                candidates.Add(UiKit.Label("投稿が足りません。先に地図から投稿を置いてみよう。", "empty"));
            else
            {
                var table = new DataTable("NO", "NAME", "DIST");
                foreach (var drop in app.Api.Drops.OrderBy(app.DistanceTo).Take(80))
                {
                    var index = picked.IndexOf(drop.id);
                    table.AddRow(new[] { index < 0 ? "+" : (index + 1).ToString("00"), drop.teaser,
                        app.HasLocation ? AppFormat.Distance(app.DistanceTo(drop)) : "—" }, () =>
                    {
                        if (busy) return;
                        if (picked.Contains(drop.id)) picked.Remove(drop.id);
                        else if (picked.Count < 20) picked.Add(drop.id);
                        else app.Toast("選べる投稿は20か所までです。");
                        RefreshPicker();
                    }, index >= 0);
                }
                candidates.Add(table);
            }
            UpdateSubmit();
        }

        private void UpdateSubmit()
        {
            submit?.SetEnabled(!busy && !string.IsNullOrWhiteSpace(title?.value) && picked.Count >= 2 && picked.Count <= 20);
            if (submit != null) submit.text = busy ? "作成しています…" : "この順でイベントを作る";
        }

        private void Move(int index, int delta)
        {
            var target = index + delta;
            if (target < 0 || target >= picked.Count || busy) return;
            var value = picked[index];
            picked[index] = picked[target];
            picked[target] = value;
            RefreshPicker();
        }

        private static int StatusRank(KokoEvent ev)
        {
            var status = AppFormat.EventStatus(ev).ToLowerInvariant();
            return status == "live" ? 1 : status == "upcoming" ? 2 : 3;
        }

        private static string StatusLabel(string status) =>
            status == "live" ? "LIVE / 開催中" : status == "upcoming" ? "UPCOMING / これから" : "ENDED / 終了";

        private static string Period(KokoEvent ev)
        {
            if (!ev.startAt.HasValue && !ev.endAt.HasValue) return "ANYTIME / いつでも";
            return (ev.startAt?.ToLocalTime().ToString("MM/dd HH:mm") ?? "—") + " → " +
                (ev.endAt?.ToLocalTime().ToString("MM/dd HH:mm") ?? "—");
        }
    }
}




