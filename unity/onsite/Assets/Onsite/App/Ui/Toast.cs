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
            var mark = UiKit.Label("○", "toast-mark mono"); mark.pickingMode = PickingMode.Ignore; Add(mark);
            var copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.Add(UiKit.Label("FIELD / MESSAGE", "toast-kicker mono"));
            message = UiKit.Label("", "toast-message"); message.pickingMode = PickingMode.Ignore;
            copy.Add(message); Add(copy);
        }
        public void Show(string text)
        {
            message.text = text ?? "通信を確認してください";
            RemoveFromClassList("toast-hidden"); dismiss?.Pause();
            Anim.Enter(this);
            dismiss = schedule.Execute(() => { AddToClassList("toast-hidden"); style.opacity = 0; });
            dismiss.StartingIn(4800);
        }
    }
}
