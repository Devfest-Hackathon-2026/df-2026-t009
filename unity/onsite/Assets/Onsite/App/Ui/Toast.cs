using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    public sealed class Toast : VisualElement
    {
        readonly Label message;
        IVisualElementScheduledItem dismiss;
        public Toast()
        {
            AddToClassList("toast"); AddToClassList("toast-hidden"); pickingMode = PickingMode.Ignore;
            Add(new CornerTicks()); message = UiKit.Label("", "toast-message"); message.pickingMode = PickingMode.Ignore; Add(message);
        }
        public void Show(string text)
        {
            message.text = text ?? "通信を確認してください";
            RemoveFromClassList("toast-hidden"); dismiss?.Pause();
            dismiss = schedule.Execute(() => AddToClassList("toast-hidden"));
            dismiss.ExecuteLater(4800);
        }
    }
}

