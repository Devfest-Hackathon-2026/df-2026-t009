using System;
using System.Globalization;
using Onsite.App;
using Onsite.Map;
using Onsite.Net;
using UnityEngine;

public static class HelperContracts
{
    static int assertions;
    static void Check(bool value, string message)
    {
        assertions++;
        if (!value) throw new Exception(message);
    }

    public static void Main()
    {
        var clock = new DateTime(2026, 11, 8, 3, 59, 0, DateTimeKind.Local);
        var trail = new TrailStore(() => clock);
        Check(trail.Day == "2026-11-07", "04:00 day boundary before rollover");
        Check(trail.Record(35, 139), "First valid position recorded");
        Check(!trail.Record(35.00001, 139), "GPS movement below 15 m discarded");
        Check(!trail.Record(double.NaN, 139), "Invalid coordinates discarded");
        Check(trail.Record(35.0002, 139), "Movement over 15 m recorded");
        Check(trail.Distance > 22 && trail.Distance < 23, "Geodesic distance accumulated");
        var reloaded = new TrailStore(() => clock);
        Check(reloaded.Points.Count == 2 && Math.Abs(reloaded.Distance - trail.Distance) < 0.001, "JSON daily storage reload");
        Check(trail.Record(36, 139) && trail.Distance < 23, "Warp excluded from walking distance");
        clock = clock.AddMinutes(1);
        Check(trail.CheckDay() && trail.Day == "2026-11-08" && trail.Points.Count == 0, "Day switches at 04:00");
        Check(!trail.CheckDay(), "Unchanged day does not reload");
        for (var i = 0; i < 18; i++) { trail.Record(35, 139); clock = clock.AddDays(1); trail.CheckDay(); }
        Check(!PlayerPrefs.Data.ContainsKey("onsite.trail.2026-11-07"), "Old trails pruned after 14 recorded days");
        PlayerPrefs.SetString("onsite.auth", "keep");
        trail.Reset();
        Check(PlayerPrefs.GetString("onsite.auth") == "keep", "Trail reset preserves authentication/settings");
        Check(trail.Points.Count == 0 && trail.Distance == 0 && PlayerPrefs.Data.Count == 1, "Trail reset removes indexed trail keys");
        PlayerPrefs.SetString("onsite.trails.index", "broken json");
        Check(new TrailStore(() => clock).Points.Count == 0, "Corrupt saved JSON recovers safely");
        var level = AppFormat.LevelOf(new Profile { opened = 3, placed = 1, likesReceived = 1, comments = 5 });
        Check(level.Xp == 118 && level.Level == 3 && level.Progress == 0 && level.ToNext == 92, "XP weights and JS Math.round threshold");
        var beginning = AppFormat.LevelOf(null);
        Check(beginning.Level == 1 && beginning.ToNext == 50, "Empty profile has level one");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        Check(AppFormat.Distance(1250) == "1.3 km", "Distance uses invariant numeric formatting");
        Check(AppFormat.Coordinates(-35.1, -139.2) == "35.10000 S 139.20000 W", "Coordinate hemispheres and decimal separator");
        Check(AppFormat.EventStatus(new KokoEvent { startAt = DateTime.UtcNow.AddHours(1) }) == "UPCOMING", "Future event status");
        Check(AppFormat.EventStatus(new KokoEvent { endAt = DateTime.UtcNow.AddHours(-1) }) == "ENDED", "Ended event status");
        Check(AppFormat.EventStatus(new KokoEvent()) == "LIVE", "Undated event is live");
        var route = new KokoEvent { stops = new[] { new EventStop { lat = 35, lng = 139 }, new EventStop { lat = 35.001, lng = 139 } } };
        Check(AppFormat.RouteLength(route) > 111 && AppFormat.RouteLength(route) < 112, "Route length uses all ordered legs");
        Check(AppFormat.Ago(DateTime.UtcNow.AddMinutes(-3)) == "3分前", "Relative Japanese timestamp");
        Console.WriteLine("PASS: " + assertions + " helper contract assertions (in-memory PlayerPrefs and JSON shim; no Unity execution).");
    }
}
