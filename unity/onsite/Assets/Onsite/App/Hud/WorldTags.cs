using System.Collections.Generic;
using System.Linq;
using Onsite.App.Ui;
using Onsite.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Hud
{
    public sealed class WorldTags : VisualElement
    {
        sealed class Tag
        {
            public VisualElement Root, Text;
            public HexIcon Icon;
            public LaserBand Band;
            public Label Teaser, Distance;
            public bool Visible, LabelVisible, Selected;
        }
        readonly OnsiteApp app;
        readonly Dictionary<string, Tag> tags = new Dictionary<string, Tag>();
        readonly HashSet<string> nearest = new HashSet<string>();
        float nextSort;
        public WorldTags(OnsiteApp app)
        {
            this.app = app; pickingMode = PickingMode.Ignore;
            AddToClassList("world-tags");
        }
        public void Sync()
        {
            if (app.Api == null) return;
            var keep = new HashSet<string>(app.Api.Drops.Select(d => d.id));
            foreach (var id in tags.Keys.Where(id => !keep.Contains(id)).ToArray())
            { tags[id].Root.RemoveFromHierarchy(); tags.Remove(id); }
            foreach (var drop in app.Api.Drops)
            {
                if (!tags.TryGetValue(drop.id, out var tag))
                {
                    tag = new Tag();
                    tag.Root = new VisualElement { focusable = true }; tag.Root.AddToClassList("world-tag"); tag.Root.AddToClassList("ui-surface");
                    tag.Root.style.left = 0; tag.Root.style.top = 0;
                    tag.Icon = new HexIcon(); tag.Icon.pickingMode = PickingMode.Ignore;
                    tag.Text = new VisualElement(); tag.Text.AddToClassList("world-tag-label");
                    tag.Band = new LaserBand(false, true); tag.Band.AddToClassList("tag-band"); tag.Text.Add(tag.Band);
                    tag.Teaser = UiKit.Label("", "tag-teaser"); tag.Teaser.pickingMode = PickingMode.Ignore;
                    tag.Distance = UiKit.Label("", "mono distance-box"); tag.Distance.pickingMode = PickingMode.Ignore;
                    tag.Text.Add(tag.Teaser); tag.Text.Add(tag.Distance);
                    tag.Root.Add(tag.Icon); tag.Root.Add(tag.Text); tag.Root.Add(new TagLeader());
                    var id = drop.id;
                    void Select()
                    {
                        var found = app.Api?.Drops.FirstOrDefault(d => d.id == id);
                        if (found != null) app.SelectDrop(found);
                    }
                    tag.Root.RegisterCallback<ClickEvent>(_ => Select());
                    tag.Root.RegisterCallback<NavigationSubmitEvent>(evt => { Select(); evt.StopPropagation(); });
                    tags[drop.id] = tag; Add(tag.Root);
                }
                tag.Teaser.text = drop.teaser?.Length > 22 ? drop.teaser.Substring(0, 21) + "…" : drop.teaser;
                tag.Root.tooltip = drop.teaser;
            }
            nextSort = 0;
        }
        public void Project(bool visible)
        {
            style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                foreach (var tag in tags.Values) { tag.Visible = false; tag.LabelVisible = false; }
                return;
            }
            if (app.MapCamera == null || app.Markers == null || panel == null) return;
            if (Time.unscaledTime >= nextSort)
            {
                nextSort = Time.unscaledTime + 0.5f; nearest.Clear();
                if (app.Api != null) foreach (var d in app.Api.Drops.OrderBy(app.DistanceTo).Take(6)) nearest.Add(d.id);
            }
            foreach (var anchor in app.Markers.ScreenAnchors)
            {
                if (!tags.TryGetValue(anchor.drop.id, out var tag)) continue;
                var screen = app.MapCamera.WorldToScreenPoint(anchor.top);
                bool show = screen.z > 0 && screen.x >= 0 && screen.x <= UnityEngine.Screen.width && screen.y >= 0 && screen.y <= UnityEngine.Screen.height;
                tag.Root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show) { tag.Visible = false; tag.LabelVisible = false; continue; }
                var point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, UnityEngine.Screen.height - screen.y));
                // カメラ移動では描画位置だけを動かす。
                tag.Root.style.translate = new Translate(point.x - 18, point.y - 18);
                tag.Icon.State = anchor.state;
                bool selected = app.Selected?.id == anchor.drop.id;
                bool showLabel = nearest.Contains(anchor.drop.id) || selected;
                tag.Text.style.display = showLabel ? DisplayStyle.Flex : DisplayStyle.None;
                // 右端では文字だけを左へ返し、印は地面に固定する。
                bool rightEdge = point.x + 340 > resolvedStyle.width - 24;
                tag.Root.EnableInClassList("right-edge", rightEdge);
                tag.Distance.text = AppFormat.Distance(app.DistanceTo(anchor.drop));
                tag.Root.EnableInClassList("selected", selected);
                if (selected != tag.Selected) tag.Band.Amber = selected;
                if (showLabel && (!tag.LabelVisible || !tag.Visible))
                {
                    Anim.Enter(tag.Text); tag.Band.DrawIn();
                }
                if (selected && !tag.Selected) tag.Band.Flash();
                tag.Visible = true; tag.LabelVisible = showLabel; tag.Selected = selected;
            }
        }
        sealed class TagLeader : VisualElement
        {
            public TagLeader()
            {
                pickingMode = PickingMode.Ignore; AddToClassList("tag-leader");
                style.position = Position.Absolute; style.left = 17; style.top = 34; style.width = 1; style.height = 17;
                generateVisualContent += context =>
                {
                    var p = context.painter2D;
                    p.strokeColor = new Color(0.5f, 0.89f, 1f, 0.12f); p.lineWidth = 5;
                    Draw.Line(p, new Vector2(0, 0), new Vector2(0, 15));
                    p.strokeColor = new Color(0.72f, 0.95f, 1f, 0.65f); p.lineWidth = 1;
                    Draw.Line(p, new Vector2(0, 0), new Vector2(0, 15));
                };
            }
        }
    }
}
