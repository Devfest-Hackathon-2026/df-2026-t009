using System.Collections.Generic;
using System.Linq;
using Onsite.Net;
using UnityEngine;

namespace Onsite.Map
{
    public enum DropState { Locked, InRange, Opened, Mine }

    /// <summary>
    /// 投稿を地図に立てる：地面の六角形と範囲の輪、上へ伸びる光の柱。
    /// 画面上のアイコンと文字は HUD 側が <see cref="ScreenAnchors"/> を使って描く。
    /// </summary>
    public class MarkerField : MonoBehaviour
    {
        public MapWorld world;

        public static readonly Color Cyan = new(0.50f, 0.89f, 1.00f);
        public static readonly Color Amber = new(1.00f, 0.70f, 0.29f);
        public static readonly Color Dim = new(0.23f, 0.35f, 0.38f);
        public static readonly Color White = new(0.90f, 0.96f, 0.97f);

        class Marker
        {
            public Drop drop;
            public GameObject root;
            public LineRenderer ring, hex;
            public GameObject beam;
            public Material ringMat, hexMat, beamMat;
            public DropState state;
            public Vector3 top;
        }

        readonly Dictionary<string, Marker> _markers = new();
        public string SelectedId { get; set; }

        /// <summary>HUD が使う、各投稿の柱の上端（ワールド座標）と状態。</summary>
        public IEnumerable<(Drop drop, Vector3 top, DropState state)> ScreenAnchors =>
            _markers.Values.Select(m => (m.drop, m.top, m.state));

        public static Color ColorOf(DropState s) => s switch
        {
            DropState.InRange => Amber,
            DropState.Opened => Dim,
            DropState.Mine => White,
            _ => Cyan,
        };

        public static DropState StateOf(Drop d, string uid, ISet<string> openedIds, double? lat, double? lng, double acc)
        {
            if (d.authorId == uid) return DropState.Mine;
            if (openedIds.Contains(d.id)) return DropState.Opened;
            if (lat.HasValue && GeoMath.Within(GeoMath.Distance(lat.Value, lng.Value, d.lat, d.lng), d.radius, acc)) return DropState.InRange;
            return DropState.Locked;
        }

        public async void SetDrops(IReadOnlyList<Drop> drops)
        {
            var keep = new HashSet<string>(drops.Select(d => d.id));
            foreach (var id in _markers.Keys.Where(k => !keep.Contains(k)).ToList())
            {
                Destroy(_markers[id].root);
                _markers.Remove(id);
            }
            foreach (var d in drops)
            {
                if (!_markers.TryGetValue(d.id, out var m)) _markers[d.id] = m = Create(d);
                m.drop = d;
            }
            if (world != null && world.HasOrigin)
            {
                await world.Prefetch(drops.Select(d => (d.lat, d.lng)).ToList());
                Relayout();
            }
        }

        public void Relayout()
        {
            if (world == null || !world.HasOrigin) return;
            foreach (var m in _markers.Values)
            {
                var p = world.OnGround(m.drop.lat, m.drop.lng, 0.6f);
                m.root.transform.position = p;
                Shapes.Ring(m.ring, p, (float)m.drop.radius, 128);
                Shapes.Ring(m.hex, p, 9f, 6, Mathf.PI / 6);
                m.top = p + Vector3.up * 70f;
            }
        }

        public void UpdateStates(string uid, ISet<string> openedIds, double? lat, double? lng, double acc)
        {
            foreach (var m in _markers.Values)
            {
                m.state = StateOf(m.drop, uid, openedIds, lat, lng, acc);
                bool sel = m.drop.id == SelectedId;
                var c = ColorOf(m.state);
                float k = m.state == DropState.Opened ? 0.6f : 2.2f;
                m.hexMat.SetColor("_Color", c * (sel ? 4f : k));
                m.beamMat.SetColor("_Color", c * (sel ? 2.5f : k * 0.6f));
                m.ringMat.SetColor("_Color", c * (sel || m.state == DropState.InRange ? 1.4f : 0.35f));
                m.ringMat.SetFloat("_Pulse", m.state == DropState.InRange ? 3f : 0f);
                m.beam.SetActive(m.state != DropState.Opened || sel);
            }
        }

        Marker Create(Drop d)
        {
            var root = new GameObject($"Drop {d.id}");
            root.transform.SetParent(transform, false);
            var m = new Marker
            {
                drop = d,
                root = root,
                ringMat = Shapes.GlowMaterial(Cyan * 0.4f),
                hexMat = Shapes.GlowMaterial(Cyan * 2f),
                beamMat = Shapes.GlowMaterial(Cyan, fadeY: 1f),
            };
            m.ring = Shapes.Line(root.transform, "Range", m.ringMat, 1.2f);
            m.hex = Shapes.Line(root.transform, "Hex", m.hexMat, 2.2f);
            m.beam = Shapes.Beam(root.transform, m.beamMat, 70f, 3.2f);
            return m;
        }
    }
}
