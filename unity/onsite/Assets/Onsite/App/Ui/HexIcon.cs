using Onsite.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    public sealed class HexIcon : VisualElement
    {
        readonly Label glyph;
        readonly IVisualElementScheduledItem pulseAnimation;
        DropState state;
        float pulse = 1;
        public DropState State
        {
            get => state;
            set
            {
                if (state == value && glyph.text.Length > 0) return;
                state = value;
                glyph.text = value == DropState.InRange ? "!" : value == DropState.Opened ? "✓" : value == DropState.Mine ? "★" : "?";
                glyph.style.color = MarkerField.ColorOf(value);
                if (value == DropState.InRange) pulseAnimation?.Resume();
                else { pulseAnimation?.Pause(); pulse = 1; }
                MarkDirtyRepaint();
            }
        }

        public HexIcon(DropState state = DropState.Locked)
        {
            AddToClassList("hex-icon");
            glyph = UiKit.Label("", "hex-glyph mono"); glyph.pickingMode = PickingMode.Ignore; Add(glyph);
            // 範囲内だけ時刻を進め、見えない印の定期処理を止める。
            pulseAnimation = schedule.Execute(() =>
            {
                if (State != DropState.InRange) return;
                pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 4f); MarkDirtyRepaint();
            }).Every(50);
            pulseAnimation.Pause();
            State = state;
            generateVisualContent += context =>
            {
                var p = context.painter2D; var c = MarkerField.ColorOf(State);
                c.a = State == DropState.InRange ? pulse : 0.9f;
                p.strokeColor = c; p.fillColor = new Color(c.r, c.g, c.b, 0.08f); p.lineWidth = 1.5f;
                var center = contentRect.center; var r = Mathf.Min(contentRect.width, contentRect.height) * 0.43f;
                Draw.Polygon(p, center, r, 6, Mathf.PI / 6, true); Draw.Polygon(p, center, r, 6, Mathf.PI / 6);
            };
        }
    }
}

