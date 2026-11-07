using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;

namespace Onsite.Map
{
    /// <summary>
    /// 3D の地図。Cesium の原点を最初の現在地に置き、Google の Photorealistic 3D Tiles を読む。
    /// 緯度経度 ⇔ Unity 座標の変換と、地面の高さの取得をまとめて受け持つ。
    /// </summary>
    public class MapWorld : MonoBehaviour
    {
        public CesiumGeoreference georeference;
        public Cesium3DTileset tileset;

        public bool HasOrigin { get; private set; }
        public double OriginLat => georeference.latitude;
        public double OriginLng => georeference.longitude;

        // 高さの問い合わせは重いので、約 10m 単位でまとめて覚えておく
        readonly Dictionary<long, double> _heightCache = new();
        double _originGroundHeight;

        public event Action OriginChanged;

        void Awake()
        {
            var s = OnsiteSecrets.Load();
            if (tileset != null && s != null && !string.IsNullOrEmpty(s.mapsApiKey))
            {
                tileset.tilesetSource = CesiumDataSource.FromUrl;
                tileset.url = s.GoogleTilesUrl;
                tileset.showCreditsOnScreen = true; // Google の表記は必須
                tileset.maximumScreenSpaceError = Application.isMobilePlatform ? 24 : 16;
                tileset.createPhysicsMeshes = false; // 物理は使わない（重いので作らない）
            }
        }

        /// <summary>原点を移す。遠くへワープしたときや、最初の現在地が分かったとき。</summary>
        public async void SetOrigin(double lat, double lng)
        {
            georeference.SetOriginLongitudeLatitudeHeight(lng, lat, 0);
            _heightCache.Clear();
            HasOrigin = true;
            _originGroundHeight = 0;
            OriginChanged?.Invoke();
            var h = await GroundHeight(lat, lng);
            if (h.HasValue)
            {
                // 原点を地面の高さに合わせ、Unity の y=0 がだいたい地表になるようにする
                _originGroundHeight = h.Value;
                georeference.SetOriginLongitudeLatitudeHeight(lng, lat, h.Value);
                _heightCache.Clear();
                OriginChanged?.Invoke();
            }
        }

        /// <summary>緯度経度（と標高）を Unity のワールド座標に。</summary>
        public Vector3 ToUnity(double lat, double lng, double height)
        {
            var ecef = georeference.ellipsoid.LongitudeLatitudeHeightToCenteredFixed(new double3(lng, lat, height));
            var p = georeference.TransformEarthCenteredEarthFixedPositionToUnity(ecef);
            return georeference.transform.TransformPoint(new Vector3((float)p.x, (float)p.y, (float)p.z));
        }

        /// <summary>地表の上に置くときの Unity 座標。高さが分からなければ原点の高さを使う。</summary>
        public Vector3 OnGround(double lat, double lng, float lift = 0)
        {
            double h = _heightCache.TryGetValue(Key(lat, lng), out var v) ? v : _originGroundHeight;
            return ToUnity(lat, lng, h + lift);
        }

        public (double lat, double lng) FromUnity(Vector3 world)
        {
            var local = georeference.transform.InverseTransformPoint(world);
            var ecef = georeference.TransformUnityPositionToEarthCenteredEarthFixed(new double3(local.x, local.y, local.z));
            var llh = georeference.ellipsoid.CenteredFixedToLongitudeLatitudeHeight(ecef);
            return (llh.y, llh.x);
        }

        /// <summary>3D タイルから地表の高さを調べる（屋上ではなく、その地点の一番上の面）。</summary>
        public async Task<double?> GroundHeight(double lat, double lng)
        {
            if (_heightCache.TryGetValue(Key(lat, lng), out var cached)) return cached;
            if (tileset == null) return null;
            try
            {
                var r = await tileset.SampleHeightMostDetailed(new double3(lng, lat, 0));
                if (r.sampleSuccess != null && r.sampleSuccess.Length > 0 && r.sampleSuccess[0])
                {
                    double h = r.longitudeLatitudeHeightPositions[0].z;
                    _heightCache[Key(lat, lng)] = h;
                    return h;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[onsite] 高さを取れませんでした: {e.Message}");
            }
            return null;
        }

        /// <summary>まとめて高さを調べ、キャッシュに入れる（投稿の印を地面に立てるため）。</summary>
        public async Task Prefetch(IReadOnlyList<(double lat, double lng)> points)
        {
            if (tileset == null || points.Count == 0) return;
            var need = new List<double3>();
            foreach (var p in points) if (!_heightCache.ContainsKey(Key(p.lat, p.lng))) need.Add(new double3(p.lng, p.lat, 0));
            if (need.Count == 0) return;
            try
            {
                var r = await tileset.SampleHeightMostDetailed(need.ToArray());
                for (int i = 0; i < need.Count; i++)
                    if (r.sampleSuccess[i]) _heightCache[Key(need[i].y, need[i].x)] = r.longitudeLatitudeHeightPositions[i].z;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[onsite] 高さをまとめて取れませんでした: {e.Message}");
            }
        }

        static long Key(double lat, double lng) => ((long)Math.Round(lat * 10000)) * 10_000_000L + (long)Math.Round(lng * 10000);
    }
}
