using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    public sealed class CornerTicks : VisualElement
    {
        float progress;
        readonly IVisualElementScheduledItem animation;
        public CornerTicks()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute; style.left = 0; style.right = 0; style.top = 0; style.bottom = 0;
            float started = 0;
            animation = schedule.Execute(() =>
            {
                progress = 1 - Mathf.Pow(1 - Mathf.Clamp01((Time.unscaledTime - started) / .28f), 3);
                MarkDirtyRepaint(); if (progress >= 1) animation.Pause();
            }).Every(16); animation.Pause();
            RegisterCallback<AttachToPanelEvent>(_ => { started = Time.unscaledTime; progress = 0; animation.Resume(); });
            RegisterCallback<DetachFromPanelEvent>(_ => animation.Pause());
            generateVisualContent += context =>
            {
                var p = context.painter2D;
                float width = contentRect.width, height = contentRect.height, length = 14 * progress;
                // 対角の二点だけを留める。
                for (int layer = 0; layer < 2; layer++)
                {
                    p.strokeColor = new Color(.63f, .9f, 1, layer == 0 ? .09f : .45f);
                    p.lineWidth = layer == 0 ? 5 : 1;
                    p.BeginPath(); p.MoveTo(new Vector2(1, length)); p.LineTo(new Vector2(1, 1)); p.LineTo(new Vector2(length, 1)); p.Stroke();
                    p.BeginPath(); p.MoveTo(new Vector2(width - length, height - 1)); p.LineTo(new Vector2(width - 1, height - 1)); p.LineTo(new Vector2(width - 1, height - length)); p.Stroke();
                }
            };
        }
    }
}
