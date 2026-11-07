using System;

namespace Onsite.Map
{
    /// <summary>緯度経度の計算（サーバーの geo.ts と同じ式）。</summary>
    public static class GeoMath
    {
        const double R = 6371008.8;
        static double Rad(double d) => d * Math.PI / 180.0;

        public static double Distance(double lat1, double lng1, double lat2, double lng2)
        {
            double dLat = Rad(lat2 - lat1), dLng = Rad(lng2 - lng1);
            double a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(dLng / 2), 2);
            return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        }

        /// <summary>GPS の誤差を最大 30m まで差し引いて、範囲内かどうか。</summary>
        public static bool Within(double distance, double radius, double accuracy)
            => distance - Math.Min(Math.Max(accuracy, 0), 30) <= radius;

        public static (double lat, double lng) Move(double lat, double lng, double eastM, double northM)
        {
            double mLat = 111320.0, mLng = 111320.0 * Math.Cos(Rad(lat));
            return (lat + northM / mLat, lng + eastM / mLng);
        }

        public static string Format(double m) => m < 1000 ? $"{Math.Round(m)}m" : $"{m / 1000:0.0}km";
    }
}
