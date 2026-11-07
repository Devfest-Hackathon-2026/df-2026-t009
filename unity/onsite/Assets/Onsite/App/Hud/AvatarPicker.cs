using System;
using Onsite.Map;
using Onsite.App.Ui;
using UnityEngine.UIElements;

namespace Onsite.App.Hud
{
    public sealed class AvatarPicker : VisualElement
    {
        private AvatarLook look;
        private readonly Action<AvatarLook> changed;
        public AvatarPicker(AvatarLook initial, Action<AvatarLook> onChanged)
        {
            look = initial;
            changed = onChanged;
            AddToClassList("picker");
            Rebuild();
        }

        private void Rebuild()
        {
            Clear();
            AddChoice("BODY / からだ", new[] { "紳士", "レディ", "ドレス", "ゴブリン" }, (int)look.body,
                index => look.body = (BodyType)index);
            AddChoice("POSE / ポーズ", new[] { "立つ", "手をふる", "ポーズ" }, (int)look.pose,
                index => look.pose = (AvatarPose)index);
            AddChoice("FINISH / 仕上げ", new[] { "ゴールド", "シルバー", "ローズゴールド", "クリスタル" }, (int)look.finish,
                index => look.finish = (Finish)index);
        }

        private void AddChoice(string title, string[] names, int selected, Action<int> apply)
        {
            Add(UiKit.Label(title, "section-label"));
            var row = new VisualElement();
            row.AddToClassList("choice-row");
            for (var i = 0; i < names.Length; i++)
            {
                var index = i;
                var button = UiKit.Button(names[i], () =>
                {
                    apply(index);
                    changed?.Invoke(look);
                    Rebuild();
                });
                button.AddToClassList("choice");
                button.EnableInClassList("on", i == selected);
                row.Add(button);
            }
            Add(row);
        }
    }
}

