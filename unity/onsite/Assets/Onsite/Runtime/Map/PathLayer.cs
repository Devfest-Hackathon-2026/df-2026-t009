using System.Collections.Generic;
using System.Linq;
using Onsite.Net;
using UnityEngine;

namespace Onsite.Map
{
    /// <summary>自分の足あと（シアンの点線）と、イベントの巡るルート（アンバーの流れる破線）。</summary>
    public class PathLayer : MonoBehaviour
    {
        public MapWorld world;
        LineRenderer _trail, _route;
        readonly List<(double lat, double lng)> _trailPts = new();
        EventStop[] _stops = System.Array.Empty<EventStop>();

        void Awake()
        {
            _trail = Shapes.Line(transform, "Trail", Shapes.GlowMaterial(MarkerField.Cyan * 2.4f, dash: 0.6f), 2.2f);
            _route = Shapes.Line(transform, "Route", Shapes.GlowMaterial(MarkerField.Amber * 2.4f, dash: 0.15f), 3.2f);
        }

        public void SetTrail(IEnumerable<(double lat, double lng)> pts)
        {
            _trailPts.Clear();
            _trailPts.AddRange(pts);
            Relayout();
        }

        public void SetRoute(EventStop[] stops)
        {
            _stops = stops ?? System.Array.Empty<EventStop>();
            Relayout();
        }

        public void Relayout()
        {
            if (world == null || !world.HasOrigin) return;
            Shapes.Path(_trail, _trailPts.Select(p => world.OnGround(p.lat, p.lng, 1.5f)).ToList());
            Shapes.Path(_route, _stops.Select(s => world.OnGround(s.lat, s.lng, 3f)).ToList());
        }
    }
}
