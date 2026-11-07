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
            var panel = new HudPanel("INCOMING", "お知らせ", app.ShowMap);
            Add(panel);
            panel.Body.Add(UiKit.Button("すべて既読にする", () => { app.MarkNoticesRead(); Refresh(); }));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("screen-scroll");
            panel.Body.Add(scroll);
            list = scroll.contentContainer;
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
            var table = new DataTable("TYPE", "SIGNAL / ACTIVITY", "TIME");
            foreach (var n in app.Api.Notices)
            {
                var kind = n.type == "like" ? "LIKE" : n.type == "comment" ? "COMMENT" : "OPEN";
                var action = n.type == "like" ? "いいねしました" : n.type == "comment" ? "寄せ書きしました" : "現地でひらきました";
                var text = (app.IsUnread(n) ? "● " : "") + n.actorName + " さんが" + action + "\n" + n.teaser;
                if (n.type == "comment" && !string.IsNullOrEmpty(n.text)) text += "\n「" + n.text + "」";
                table.AddRow(new[] { kind, text, AppFormat.Ago(n.createdAt) }, () => Select(n), app.IsUnread(n));
            }
            list.Add(table);
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

