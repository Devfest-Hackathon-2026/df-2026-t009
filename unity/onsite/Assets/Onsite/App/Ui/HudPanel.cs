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
            AddToClassList("hud-panel");
            Header = new VisualElement(); Header.AddToClassList("panel-header");
            Header.Add(UiKit.Label(english, "section-label"));
            Header.Add(UiKit.Label(japanese, "panel-title"));
            if (close != null)
            {
                var button = UiKit.Button("×", close); button.AddToClassList("close-button");
                button.tooltip = "閉じる"; Header.Add(button);
            }
            Add(Header);
            Body = new VisualElement(); Body.AddToClassList("panel-body"); Add(Body);
            Add(new CornerTicks());
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                AddToClassList("enter");
                schedule.Execute(() => RemoveFromClassList("enter")).ExecuteLater(20);
            });
        }
    }
}
