using System;
using Onsite.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    public static class UiKit
    {
        public static Label Label(string text, string className = null)
        {
            var label = new Label(text ?? "");
            if (!string.IsNullOrEmpty(className)) foreach (var name in className.Split(' ')) if (name.Length > 0) label.AddToClassList(name);
            return label;
        }

        public static Button Button(string text, Action clicked, bool primary = false)
        {
            var button = new Button(() => Activate(clicked)) { text = text };
            button.AddToClassList("button");
            if (primary)
            {
                button.AddToClassList("primary");
                button.Insert(0, new LaserBand());
            }
            ButtonFeedback(button);
            return button;
        }

        public static void Activate(Action clicked)
        {
            Sound.ButtonPress(() =>
            {
                DeviceBridge.Feedback(HapticKind.Tap);
                clicked?.Invoke();
            });
        }

        public static void ButtonFeedback(Button button)
        {
            PressFeedback(button);
            button.clicked += () =>
            {
                Anim.Flash(button);
            };
        }

        public static void PressFeedback(VisualElement element)
        {
            element.AddToClassList("touch-feedback");
            int pressedPointer = -1;
            void Release()
            {
                pressedPointer = -1;
                element.RemoveFromClassList("touch-pressed");
            }
            element.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || !element.enabledInHierarchy || pressedPointer != -1) return;
                Sound.UserGesture();
                pressedPointer = e.pointerId;
                element.AddToClassList("touch-pressed");
            }, TrickleDown.TrickleDown);
            element.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == pressedPointer) Release(); }, TrickleDown.TrickleDown);
            element.RegisterCallback<PointerCancelEvent>(e => { if (e.pointerId == pressedPointer) Release(); }, TrickleDown.TrickleDown);
            element.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == pressedPointer) Release(); });
            element.RegisterCallback<PointerLeaveEvent>(_ => Release());
            element.RegisterCallback<DetachFromPanelEvent>(_ => Release());
        }

        public static TextField Field(string label, int max, bool multiline = false)
        {
            var field = new TextField(label) { maxLength = max, multiline = multiline };
            field.AddToClassList("field");
            if (multiline) field.AddToClassList("multiline");
            if (Onsite.Net.DeviceBridge.UsesHtmlText)
            {
                // ブラウザーでは、押すとブラウザーの入力欄が開く（キャンバス上では日本語の変換が使えないため）
                field.isReadOnly = true;
                // 入力欄そのものが先に操作を受け取ってしまうので、押した最初の段階（TrickleDown）で拾う
                field.RegisterCallback<PointerDownEvent>(e =>
                {
                    e.StopPropagation();
                    var bridge = UnityEngine.Object.FindFirstObjectByType<Onsite.Net.DeviceBridge>();
                    if (bridge == null) return;
                    System.Action<string> handler = null;
                    handler = text => { bridge.TextEdited -= handler; field.value = text ?? ""; };
                    bridge.TextEdited += handler;
                    bridge.EditText(label, field.value, max, multiline);
                }, TrickleDown.TrickleDown);
            }
            return field;
        }

        public static VisualElement Author(string name, string time = null)
        {
            var row = Row(); row.AddToClassList("author-line");
            var avatar = new VisualElement { pickingMode = PickingMode.Ignore };
            avatar.AddToClassList("author-avatar"); avatar.Add(new LineIcon("me")); row.Add(avatar);
            row.Add(Label(string.IsNullOrEmpty(name) ? "旅人" : name, "author-name"));
            if (!string.IsNullOrEmpty(time)) row.Add(Label(time, "author-time mono"));
            return row;
        }

        public static VisualElement Rule() => new LaserBand();
        public static VisualElement Row(params VisualElement[] children)
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            foreach (var child in children) if (child != null) row.Add(child);
            return row;
        }
    }

    internal static class Draw
    {
        public static void Line(Painter2D painter, Vector2 from, Vector2 to)
        {
            painter.BeginPath(); painter.MoveTo(from); painter.LineTo(to); painter.Stroke();
        }

        public static void Polygon(Painter2D painter, Vector2 center, float radius, int sides, float rotation = 0, bool fill = false)
        {
            painter.BeginPath();
            for (var i = 0; i < sides; i++)
            {
                var angle = rotation + Mathf.PI * 2 * i / sides;
                var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
            }
            painter.ClosePath();
            if (fill) painter.Fill(); else painter.Stroke();
        }

        public static void Box(Painter2D painter, Rect rect, bool fill = false)
        {
            painter.BeginPath(); painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin)); painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax)); painter.ClosePath();
            if (fill) painter.Fill(); else painter.Stroke();
        }
    }
}


