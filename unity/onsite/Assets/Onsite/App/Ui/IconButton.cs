using System;
using Onsite.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    public sealed class IconButton : Button
    {
        readonly Label badge;
        public string Badge { get => badge.text; set { badge.text = value ?? ""; badge.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex; } }
        public IconButton(string icon, string label, Action clicked) : base(clicked)
        {
            AddToClassList("icon-button"); tooltip = label;
            Add(new LineIcon(icon)); Add(UiKit.Label(label, "rail-label section-label"));
            badge = UiKit.Label("", "rail-badge mono"); Add(badge); Badge = "";
        }
    }

    public sealed class MetricChip : VisualElement
    {
        public Label Number { get; }
        public MetricChip(string icon, string value)
        {
            AddToClassList("metric-chip");
            Add(new LineIcon(icon)); Number = UiKit.Label(value, "mono"); Add(Number);
        }
    }

    public sealed class LineIcon : VisualElement
    {
        readonly string icon;
        public LineIcon(string icon)
        {
            this.icon = (icon ?? "").ToLowerInvariant(); AddToClassList("line-icon"); pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        void Paint(MeshGenerationContext context)
        {
            var p = context.painter2D; p.strokeColor = resolvedStyle.color; p.lineWidth = 1.7f;
            var s = Mathf.Min(contentRect.width, contentRect.height) / 32f;
            var origin = contentRect.center - Vector2.one * 16 * s;
            Vector2 P(float x, float y) => origin + new Vector2(x, y) * s;
            void L(float x, float y, float a, float b) => Draw.Line(p, P(x,y), P(a,b));
            void B(float x,float y,float w,float h) => Draw.Box(p,new Rect(P(x,y),new Vector2(w,h)*s));
            switch (icon)
            {
                case "map":
                    p.BeginPath(); p.MoveTo(P(3,7)); p.LineTo(P(11,4)); p.LineTo(P(21,8)); p.LineTo(P(29,5)); p.LineTo(P(29,25)); p.LineTo(P(21,28)); p.LineTo(P(11,24)); p.LineTo(P(3,27)); p.ClosePath(); p.Stroke(); L(11,4,11,24); L(21,8,21,28); break;
                case "feed": B(4,4,24,24); L(9,10,23,10); L(9,16,23,16); L(9,22,18,22); break;
                case "post": case "plus": case "+": L(16,4,16,28); L(4,16,28,16); B(3,3,26,26); break;
                case "notice":
                    L(5,24,27,24); L(8,24,8,13); L(24,24,24,13); L(8,13,12,7); L(12,7,20,7); L(20,7,24,13); L(14,28,18,28); L(16,3,16,6); break;
                case "me":
                    Draw.Polygon(p,P(16,10),6*s,16); L(5,28,5,24); L(5,24,10,20); L(10,20,22,20); L(22,20,27,24); L(27,24,27,28); break;
                case "like": case "heart":
                    p.BeginPath(); p.MoveTo(P(16,27)); p.LineTo(P(4,15)); p.LineTo(P(4,8)); p.LineTo(P(10,5)); p.LineTo(P(16,11)); p.LineTo(P(22,5)); p.LineTo(P(28,8)); p.LineTo(P(28,15)); p.ClosePath(); p.Stroke(); break;
                case "comment": case "comments":
                    B(3,5,26,18); L(8,23,8,29); L(8,29,15,23); L(8,11,24,11); L(8,17,20,17); break;
                case "foot": case "footprint": case "open": case "opened":
                    Draw.Polygon(p,P(11,10),4*s,8); Draw.Polygon(p,P(22,19),4*s,8); L(8,17,8,25); L(8,25,13,27); L(20,27,24,29); break;
                case "photo": B(3,8,26,19); B(10,4,12,4); Draw.Polygon(p,P(16,18),6*s,16); break;
                case "route":
                    Draw.Polygon(p,P(6,7),3*s,6); Draw.Polygon(p,P(25,25),3*s,6); L(9,7,24,7); L(24,7,24,16); L(24,16,8,16); L(8,16,8,25); L(8,25,22,25); break;
                case "close": L(7,7,25,25); L(7,25,25,7); break;
                default:
                    Draw.Polygon(p,P(16,16),12*s,6,Mathf.PI/6); L(16,9,16,23); L(9,16,23,16); break;
            }
        }
    }
}


