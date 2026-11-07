using System.Collections.Generic;
using System.Linq;
using Onsite.App.Ui;
using Onsite.Map;
using Onsite.Net;
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
            public Label Teaser, Distance;
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
                    tag.Root = new VisualElement(); tag.Root.AddToClassList("world-tag");
                    tag.Root.style.left = 0; tag.Root.style.top = 0;
                    tag.Icon = new HexIcon(); tag.Icon.pickingMode = PickingMode.Ignore;
                    tag.Text = new VisualElement { pickingMode = PickingMode.Ignore }; tag.Text.AddToClassList("world-tag-label");
                    tag.Teaser = UiKit.Label("", "tag-teaser"); tag.Teaser.pickingMode = PickingMode.Ignore;
                    tag.Distance = UiKit.Label("", "mono distance-box"); tag.Distance.pickingMode = PickingMode.Ignore;
                    tag.Text.Add(tag.Teaser); tag.Text.Add(tag.Distance);
                    tag.Root.Add(tag.Icon); tag.Root.Add(tag.Text); tag.Root.Add(new TagNotch());
                    var id = drop.id;
                    tag.Root.RegisterCallback<ClickEvent>(_ =>
                    {
                        var found = app.Api?.Drops.FirstOrDefault(d => d.id == id);
                        if (found != null) app.SelectDrop(found);
                    });
                    tags[drop.id] = tag; Add(tag.Root);
                }
                tag.Teaser.text = drop.teaser?.Length > 22 ? drop.teaser.Substring(0, 21) + "…" : drop.teaser;
            }
            nextSort = 0;
        }
        public void Project(bool visible)
        {
            style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible || app.MapCamera == null || app.Markers == null || panel == null) return;
            if (Time.unscaledTime >= nextSort)
            {
                nextSort = Time.unscaledTime + 0.5f;
                nearest.Clear();
                if (app.Api != null) foreach (var d in app.Api.Drops.OrderBy(app.DistanceTo).Take(6)) nearest.Add(d.id);
            }
            foreach (var anchor in app.Markers.ScreenAnchors)
            {
                if (!tags.TryGetValue(anchor.drop.id, out var tag)) continue;
                var screen = app.MapCamera.WorldToScreenPoint(anchor.top);
                bool show = screen.z > 0 && screen.x >= 0 && screen.x <= UnityEngine.Screen.width && screen.y >= 0 && screen.y <= UnityEngine.Screen.height;
                tag.Root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show) continue;
                var point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, UnityEngine.Screen.height - screen.y));
                // カメラ移動ではレイアウトを組み直さず、描画位置だけを動かす。
                tag.Root.style.translate = new Translate(point.x - 24, point.y - 24);
                tag.Icon.State = anchor.state;
                tag.Text.style.display = nearest.Contains(anchor.drop.id) || app.Selected?.id == anchor.drop.id ? DisplayStyle.Flex : DisplayStyle.None;
                tag.Distance.text = AppFormat.Distance(app.DistanceTo(anchor.drop));
                tag.Root.EnableInClassList("selected", app.Selected?.id == anchor.drop.id);
            }
        }
        sealed class TagNotch : VisualElement
        {
            public TagNotch()
            {
                pickingMode = PickingMode.Ignore; style.position = Position.Absolute;
                style.left = 15; style.top = 43; style.width = 18; style.height = 10;
                generateVisualContent += c =>
                {
                    var p = c.painter2D; p.lineWidth = 1; p.strokeColor = MarkerField.Cyan;
                    p.BeginPath(); p.MoveTo(new Vector2(0, 0)); p.LineTo(new Vector2(9, 8)); p.LineTo(new Vector2(18, 0)); p.Stroke();
                };
            }
        }
    }
}

