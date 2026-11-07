using Onsite.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    public sealed class Crosshair : VisualElement
    {
        public Crosshair()
        {
            AddToClassList("crosshair"); pickingMode = PickingMode.Ignore;
            generateVisualContent += context =>
            {
                var p = context.painter2D; p.lineWidth = 1.5f; p.strokeColor = MarkerField.Amber;
                var c = contentRect.center;
                Draw.Polygon(p,c,25,6,Mathf.PI/6);
                Draw.Line(p,c+new Vector2(-49,0),c+new Vector2(-32,0)); Draw.Line(p,c+new Vector2(32,0),c+new Vector2(49,0));
                Draw.Line(p,c+new Vector2(0,-49),c+new Vector2(0,-32)); Draw.Line(p,c+new Vector2(0,32),c+new Vector2(0,49));
                p.fillColor = MarkerField.Amber; Draw.Box(p,new Rect(c.x-2,c.y-2,4,4),true);
            };
        }
    }
}
