using Onsite.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    public sealed class CornerTicks : VisualElement
    {
        public CornerTicks()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute; style.left = -1; style.right = -1; style.top = -1; style.bottom = -1;
            generateVisualContent += context =>
            {
                var p = context.painter2D; p.strokeColor = MarkerField.Cyan; p.lineWidth = 2;
                var width = contentRect.width; var height = contentRect.height; const float length = 12;
                foreach (var c in new[] { new Vector2(1,1), new Vector2(width-1,1), new Vector2(1,height-1), new Vector2(width-1,height-1) })
                {
                    var dx = c.x < width / 2 ? length : -length; var dy = c.y < height / 2 ? length : -length;
                    p.BeginPath(); p.MoveTo(c + new Vector2(dx, 0)); p.LineTo(c); p.LineTo(c + new Vector2(0, dy)); p.Stroke();
                }
            };
        }
    }
}
