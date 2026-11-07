using Onsite.App.Ui;
using UnityEngine.UIElements;

namespace Onsite.App.Hud
{
    public sealed class OnboardingScreen : VisualElement
    {
        public OnboardingScreen(OnsiteApp app)
        {
            AddToClassList("screen");
            AddToClassList("onboarding");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("screen-scroll");
            Add(scroll);
            scroll.Add(UiKit.Label("FIELD LOG SYSTEM / 01", "section-label"));
            scroll.Add(UiKit.Label("ONSITE", "wide-wordmark"));
            scroll.Add(UiKit.Label("近づくと、ひらく。", "heading"));
            var copy = new VisualElement();
            copy.AddToClassList("onboarding-copy");
            copy.Add(UiKit.Label("地図には、その場所に残された「予告」だけ。", "body-copy"));
            copy.Add(UiKit.Label("近づくと、本文と写真がひらきます。", "body-copy"));
            copy.Add(UiKit.Label("発見を置いて、誰かの次の一歩に。", "body-copy"));
            scroll.Add(copy);
            var stage = new VisualElement { name = "avatar-stage", pickingMode = PickingMode.Ignore };
            stage.AddToClassList("avatar-stage");
            scroll.Add(stage);
            var panel = new HudPanel("YOUR FIGURE", "地図に立つ、あなたの像");
            scroll.Add(panel);
            panel.Body.Add(new AvatarPicker(app.Look, app.SetLook));
            var name = UiKit.Field("ニックネーム", 20);
            name.SetValueWithoutNotify(app.Nickname);
            panel.Body.Add(name);
            var start = UiKit.Button("この像で出発する", () => app.CompleteOnboarding(name.value.Trim()), true);
            start.SetEnabled(!string.IsNullOrWhiteSpace(name.value));
            name.RegisterValueChangedCallback(e => start.SetEnabled(!string.IsNullOrWhiteSpace(e.newValue)));
            panel.Body.Add(start);
            panel.Body.Add(UiKit.Label("現在地は近くにいるかの判定に使います。足あと共有は初期設定でオフです。", "caption"));
        }

        public void Refresh() { }
    }
}


