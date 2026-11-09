using System.Collections.Generic;
using System.Linq;
using Onsite.Net;
using UnityEngine;

namespace Onsite.Map
{
    /// <summary>
    /// 自分の足あと（シアンのレーザー、歩いた向きに光が流れる）と、
    /// イベントの巡るルート（アンバーのレーザー＋各地点を結ぶ光の粒）。
    /// </summary>
    public class PathLayer : MonoBehaviour
    {
        public MapWorld world;
        LineRenderer _trail, _trailGlow, _route, _routeGlow;
        readonly List<(double lat, double lng)> _trailPts = new();
        EventStop[] _stops = System.Array.Empty<EventStop>();

        void Awake()
        {
            // 太いにじみの線と、細い芯の線を重ねてレーザーに見せる
            _trailGlow = Shapes.Line(transform, "Trail Glow", Shapes.LaserMaterial(MarkerField.Cyan * 0.55f, core: 0), 1f);
            _trail = Shapes.Line(transform, "Trail", Shapes.LaserMaterial(MarkerField.Cyan * 2.2f, flow: 0.03f, flowSpeed: 0.8f), 1f);
            _routeGlow = Shapes.Line(transform, "Route Glow", Shapes.LaserMaterial(MarkerField.Amber * 0.6f, core: 0), 1f);
            _route = Shapes.Line(transform, "Route", Shapes.LaserMaterial(MarkerField.Amber * 2.6f, flow: 0.04f, flowSpeed: 1.6f, core: 1.2f), 1f);
            Shapes.KeepScreenWidth(_trailGlow, 9f);
            Shapes.KeepScreenWidth(_trail, 2.4f);
            Shapes.KeepScreenWidth(_routeGlow, 11f);
            Shapes.KeepScreenWidth(_route, 3f);
            foreach (var l in new[] { _trail, _trailGlow, _route, _routeGlow }) { l.numCornerVertices = 4; l.numCapVertices = 4; }
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
            var trail = Smooth(_trailPts.Select(p => world.OnGround(p.lat, p.lng, 2f)).ToList());
            Shapes.Path(_trail, trail); Shapes.Path(_trailGlow, trail);
            // ルートは地点どうしを、少し持ち上げた弧で結ぶ（地形に埋もれず、巡る順が読める）
            var route = Arcs(_stops.Select(s => world.OnGround(s.lat, s.lng, 3f)).ToList());
            Shapes.Path(_route, route); Shapes.Path(_routeGlow, route);
        }

        static List<Vector3> Arcs(List<Vector3> stops)
        {
            var o = new List<Vector3>();
            for (int i = 0; i + 1 < stops.Count; i++)
            {
                var a = stops[i]; var b = stops[i + 1];
                float h = Mathf.Clamp(Vector3.Distance(a, b) * 0.12f, 6f, 120f);
                for (int s = 0; s < 24; s++)
                {
                    float t = s / 24f;
                    o.Add(Vector3.Lerp(a, b, t) + Vector3.up * (4 * h * t * (1 - t)));
                }
            }
            if (stops.Count > 0) o.Add(stops[^1]);
            return o;
        }

        // GPS のガタつきを少しならす（線がギザギザだと安っぽく見える）
        static List<Vector3> Smooth(List<Vector3> p)
        {
            if (p.Count < 3) return p;
            var o = new List<Vector3>(p.Count) { p[0] };
            for (int i = 1; i < p.Count - 1; i++) o.Add(p[i - 1] * 0.25f + p[i] * 0.5f + p[i + 1] * 0.25f);
            o.Add(p[^1]);
            return o;
        }
    }
}
