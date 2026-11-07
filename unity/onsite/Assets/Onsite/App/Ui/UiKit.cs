using System;
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
            var button = new Button(clicked) { text = text };
            button.AddToClassList("button");
            if (primary) button.AddToClassList("primary");
            return button;
        }

        public static TextField Field(string label, int max, bool multiline = false)
        {
            var field = new TextField(label) { maxLength = max, multiline = multiline };
            field.AddToClassList("field");
            if (multiline) field.AddToClassList("multiline");
            if (multiline) field.AddToClassList("multiline");
            return field;
        }

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


