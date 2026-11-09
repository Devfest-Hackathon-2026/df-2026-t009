using System;
using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    public class HudPanel : VisualElement
    {
        public VisualElement Body { get; }
        public VisualElement Header { get; }
        public HudPanel(string english, string japanese, Action close = null)
        {
            AddToClassList("hud-panel"); AddToClassList("ui-surface");
            Add(new FadeSurface());
            Header = new VisualElement(); Header.AddToClassList("panel-header");
            Header.Add(UiKit.Label(english, "panel-heading"));
            Header.Add(UiKit.Label(japanese, "panel-title"));
            if (close != null)
            {
                var button = UiKit.Button("閉じる", close); button.AddToClassList("close-button");
                button.Insert(0, new LineIcon("close")); button.tooltip = "閉じる"; Header.Add(button);
            }
            Add(Header);
            var rule = new LaserBand { Scan = true }; rule.AddToClassList("panel-rule"); Add(rule);
            Body = new VisualElement(); Body.AddToClassList("panel-body"); Add(Body);
            var corners = new CornerTicks(); corners.AddToClassList("panel-corners"); Add(corners);
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Anim.Enter(Body, 30);
                Anim.Enter(Header);
            });
        }
    }
}
