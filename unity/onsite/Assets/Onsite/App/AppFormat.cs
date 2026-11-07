using System;
using System.Globalization;
using Onsite.Map;
using Onsite.Net;

namespace Onsite.App
{
    public readonly struct LevelInfo
    {
        public readonly long Xp;
        public readonly int Level;
        public readonly float Progress;
        public readonly long ToNext;
        public LevelInfo(long xp, int level, float progress, long toNext)
        { Xp = xp; Level = level; Progress = progress; ToNext = toNext; }
    }

    public static class AppFormat
    {
        public static string Distance(double meters)
        {
            if (double.IsInfinity(meters) || double.IsNaN(meters)) return "— m";
            return meters < 1000 ? Math.Round(Math.Max(0, meters)).ToString("0", CultureInfo.InvariantCulture) + " m" :
                (meters / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " km";
        }

        public static string Coordinates(double lat, double lng) =>
            Math.Abs(lat).ToString("0.00000", CultureInfo.InvariantCulture) + (lat < 0 ? " S " : " N ") +
            Math.Abs(lng).ToString("0.00000", CultureInfo.InvariantCulture) + (lng < 0 ? " W" : " E");

        public static string Ago(DateTime? date)
        {
            if (!date.HasValue) return "たった今";
            var seconds = Math.Max(0, (DateTime.UtcNow - date.Value.ToUniversalTime()).TotalSeconds);
            if (seconds < 60) return "たった今";
            if (seconds < 3600) return (int)(seconds / 60) + "分前";
            if (seconds < 86400) return (int)(seconds / 3600) + "時間前";
            if (seconds < 604800) return (int)(seconds / 86400) + "日前";
            return date.Value.ToLocalTime().ToString("M/d", CultureInfo.InvariantCulture);
        }

        public static bool Today(DateTime? date) => date.HasValue && TrailStore.DayKey(date.Value) == TrailStore.DayKey(DateTime.Now);

        public static string EventStatus(KokoEvent route)
        {
            if (route == null) return "UNKNOWN";
            var now = DateTime.UtcNow;
            if (route.endAt.HasValue && route.endAt.Value.ToUniversalTime() < now) return "ENDED";
            if (route.startAt.HasValue && route.startAt.Value.ToUniversalTime() > now) return "UPCOMING";
            return "LIVE";
        }

        public static double RouteLength(KokoEvent route)
        {
            double length = 0;
            var stops = route?.stops;
            if (stops == null) return length;
            for (var i = 1; i < stops.Length; i++)
                if (stops[i - 1] != null && stops[i] != null)
                    length += GeoMath.Distance(stops[i - 1].lat, stops[i - 1].lng, stops[i].lat, stops[i].lng);
            return length;
        }

        public static LevelInfo LevelOf(Profile profile)
        {
            // Web と同じ重み・四捨五入で、画面を移ってもレベルを揃える。
            var xp = (profile?.opened ?? 0) * 30 + (profile?.placed ?? 0) * 15 +
                (profile?.opensReceived ?? 0) * 10 + (profile?.likesReceived ?? 0) * 3 + (profile?.comments ?? 0) * 2;
            var rest = xp; long need = 50; var level = 1;
            while (rest >= need)
            {
                rest -= need;
                level++;
                need = (long)Math.Floor(need * 1.35 + 0.5);
            }
            return new LevelInfo(xp, level, (float)((double)rest / need), need - rest);
        }
    }
}
