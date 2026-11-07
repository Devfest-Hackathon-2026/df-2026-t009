// Cesium パッケージ未インポート時も、既存の地図契約に対して UI を型検査する。
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
namespace Onsite.Map
{
    public class MapWorld : MonoBehaviour
    {
        public bool HasOrigin { get; private set; }
        public double OriginLat { get; private set; }
        public double OriginLng { get; private set; }
        public event Action OriginChanged;
        public void SetOrigin(double lat, double lng) { OriginLat = lat; OriginLng = lng; HasOrigin = true; OriginChanged?.Invoke(); }
        public Vector3 ToUnity(double lat, double lng, double height) => default;
        public Vector3 OnGround(double lat, double lng, float lift = 0) => default;
        public (double lat, double lng) FromUnity(Vector3 world) => default;
        public Task<double?> GroundHeight(double lat, double lng) => Task.FromResult<double?>(null);
        public Task Prefetch(IReadOnlyList<(double lat, double lng)> points) => Task.CompletedTask;
    }
}
namespace UnityEngine.EventSystems { public class EventSystem { } }
