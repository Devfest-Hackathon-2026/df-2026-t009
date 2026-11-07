using Onsite.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    public sealed class SegmentBar : VisualElement
    {
        float value;
        Color tint = MarkerField.Cyan;
        public int Cells { get; set; } = 24;
        public float Value { get => value; set { this.value = Mathf.Clamp01(value); MarkDirtyRepaint(); } }
        public Color Tint { get => tint; set { tint = value; MarkDirtyRepaint(); } }
        public SegmentBar(int cells = 24)
        {
            Cells = cells; AddToClassList("segment-bar"); pickingMode = PickingMode.Ignore;
            generateVisualContent += context =>
            {
                var p = context.painter2D; var n = Mathf.Max(1, Cells); var w = contentRect.width / n;
                for (var i = 0; i < n; i++)
                {
                    p.fillColor = i < Mathf.CeilToInt(Value * n) ? Tint : new Color(Tint.r, Tint.g, Tint.b, 0.13f);
                    Draw.Box(p, new Rect(i * w, 0, Mathf.Max(1, w - 3), contentRect.height), true);
                }
            };
        }
    }
}
