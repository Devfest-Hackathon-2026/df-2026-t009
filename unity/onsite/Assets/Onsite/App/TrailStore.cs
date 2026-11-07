using System;
using System.Collections.Generic;
using System.Globalization;
using Onsite.Map;
using UnityEngine;

namespace Onsite.App
{
    [Serializable]
    public sealed class TrailPoint
    {
        public double lat;
        public double lng;
        public long time;
    }

    /// <summary>足あとは端末にだけ保存し、共有の送信判断はアプリが受け持つ。</summary>
    public sealed class TrailStore
    {
        [Serializable] public sealed class DayData { public List<TrailPoint> points = new List<TrailPoint>(); }
        [Serializable] public sealed class DayIndex { public List<string> days = new List<string>(); }
        const string Prefix = "onsite.trail.";
        const string IndexKey = "onsite.trails.index";
        const int MaxDays = 14;
        const int MaxPoints = 3000;
        readonly Func<DateTime> now;
        DayIndex index;
        public string Day { get; private set; }
        public List<TrailPoint> Points { get; private set; } = new List<TrailPoint>();
        public double Distance { get; private set; }

        public TrailStore(Func<DateTime> clock = null)
        {
            now = clock ?? (() => DateTime.Now);
            index = Read<DayIndex>(PlayerPrefs.GetString(IndexKey, "")) ?? new DayIndex();
            if (index.days == null) index.days = new List<string>();
            CheckDay();
        }

        public static string DayKey(DateTime date) => date.ToLocalTime().AddHours(-4).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public bool CheckDay()
        {
            var day = DayKey(now());
            if (day == Day) return false;
            Day = day;
            var data = Read<DayData>(PlayerPrefs.GetString(Prefix + Day, ""));
            Points = data?.points ?? new List<TrailPoint>();
            // 壊れた保存値を地図へ渡さない。
            Points.RemoveAll(p => p == null || !Valid(p.lat, p.lng));
            if (Points.Count > MaxPoints) Points.RemoveRange(0, Points.Count - MaxPoints);
            Recalculate();
            return true;
        }

        public bool Record(double lat, double lng)
        {
            CheckDay();
            if (!Valid(lat, lng)) return false;
            var previous = Points.Count > 0 ? Points[Points.Count - 1] : null;
            var step = previous == null ? 0 : GeoMath.Distance(previous.lat, previous.lng, lat, lng);
            if (previous != null && step < 15) return false;
            Points.Add(new TrailPoint { lat = lat, lng = lng, time = new DateTimeOffset(now()).ToUnixTimeMilliseconds() });
            if (Points.Count > MaxPoints)
            {
                Points.RemoveAt(0);
                Recalculate();
            }
            else if (step < 2000) Distance += step; // デモのワープで歩行距離を増やさない。
            Save();
            return true;
        }

        public void Reset()
        {
            foreach (var day in index.days) PlayerPrefs.DeleteKey(Prefix + day);
            if (!string.IsNullOrEmpty(Day)) PlayerPrefs.DeleteKey(Prefix + Day);
            PlayerPrefs.DeleteKey(IndexKey);
            index = new DayIndex();
            Points.Clear();
            Distance = 0;
            Day = DayKey(now());
            PlayerPrefs.Save();
        }

        void Save()
        {
            PlayerPrefs.SetString(Prefix + Day, JsonUtility.ToJson(new DayData { points = Points }));
            if (!index.days.Contains(Day)) index.days.Add(Day);
            index.days.Sort(StringComparer.Ordinal);
            while (index.days.Count > MaxDays)
            {
                PlayerPrefs.DeleteKey(Prefix + index.days[0]);
                index.days.RemoveAt(0);
            }
            PlayerPrefs.SetString(IndexKey, JsonUtility.ToJson(index));
            PlayerPrefs.Save();
        }

        void Recalculate()
        {
            Distance = 0;
            for (var i = 1; i < Points.Count; i++)
            {
                var a = Points[i - 1]; var b = Points[i];
                var step = GeoMath.Distance(a.lat, a.lng, b.lat, b.lng);
                if (step < 2000) Distance += step;
            }
        }

        static bool Valid(double lat, double lng) => !double.IsNaN(lat) && !double.IsNaN(lng) &&
            !double.IsInfinity(lat) && !double.IsInfinity(lng) && lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180;

        static T Read<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonUtility.FromJson<T>(json); }
            catch (ArgumentException) { return null; }
        }
    }
}
