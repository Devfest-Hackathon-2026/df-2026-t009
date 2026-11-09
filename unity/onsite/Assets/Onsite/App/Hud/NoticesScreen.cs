using System.Linq;
using Onsite.App.Ui;
using Onsite.Net;
using UnityEngine.UIElements;

namespace Onsite.App.Hud
{
    public sealed class NoticesScreen : VisualElement
    {
        private readonly OnsiteApp app;
        private readonly VisualElement list;
        public NoticesScreen(OnsiteApp app)
        {
            this.app = app;
            AddToClassList("screen");
            AddToClassList("notices-screen");
            var panel = new HudPanel("INCOMING", "お知らせ", app.ShowMap);
            Add(panel);
            panel.Body.Add(UiKit.Button("すべて既読にする", () => { app.MarkNoticesRead(); Refresh(); }));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("screen-scroll");
            panel.Body.Add(scroll);
            list = scroll.contentContainer;
            list.AddToClassList("activity-list");
            Refresh();
        }

        public void Refresh()
        {
            list.Clear();
            if (app.Api == null || app.Api.Notices.Count == 0)
            {
                list.Add(UiKit.Label("まだお知らせはありません。置いた投稿への反応がここに届きます。", "empty"));
                return;
            }
            foreach (var n in app.Api.Notices)
            {
                var notice = n;
                var kind = n.type == "like" ? "LIKE" : n.type == "comment" ? "COMMENT" : "OPEN";
                var action = n.type == "like" ? "いいねしました" : n.type == "comment" ? "寄せ書きしました" : "現地でひらきました";
                var row = UiKit.Button("", () => Select(notice)); row.AddToClassList("notice-entry");
                row.EnableInClassList("unread", app.IsUnread(n));
                var meta = UiKit.Row(UiKit.Label(kind, "mono notice-kicker"));
                if (app.IsUnread(n))
                {
                    var dot = new VisualElement(); dot.AddToClassList("notice-dot"); meta.Add(dot);
                    meta.Add(UiKit.Label("NEW", "mono notice-kicker"));
                }
                row.Add(meta);
                row.Add(UiKit.Author(n.actorName, AppFormat.Ago(n.createdAt)));
                row.Add(UiKit.Label(action, "caption"));
                row.Add(UiKit.Label(n.teaser, "heading notice-title"));
                if (n.type == "comment" && !string.IsNullOrEmpty(n.text))
                    row.Add(UiKit.Label("「" + n.text + "」", "body-copy notice-quote"));
                row.Add(UiKit.Rule()); list.Add(row);
            }
        }

        private void Select(Notice notice)
        {
            var drop = app.Api.Drops.FirstOrDefault(d => d.id == notice.dropId);
            app.MarkNoticesRead();
            if (drop != null) app.SelectDrop(drop);
            else { app.Toast("この投稿は削除されたか、現在の一覧にありません。"); Refresh(); }
        }
    }
}

