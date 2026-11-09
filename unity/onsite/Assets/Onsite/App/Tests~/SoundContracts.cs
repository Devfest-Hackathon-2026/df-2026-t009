using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Onsite.App;

static class SoundContracts
{
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static int Main()
    {
        var method = typeof(Sound).GetMethod("GeneratePcm", BindingFlags.NonPublic | BindingFlags.Static);
        var generate = (Func<Dictionary<string, float[]>>)Delegate.CreateDelegate(typeof(Func<Dictionary<string, float[]>>), method);
        var clock = Stopwatch.StartNew();
        var pcm = generate();
        clock.Stop();
        double coldMs = clock.Elapsed.TotalMilliseconds;
        string[] ids = { "tap", "select", "open_panel", "close_panel", "arrival", "verify", "unlock", "complete", "like", "send", "error", "ambient" };
        Require(pcm.Count == ids.Length, "All twelve sound ids must be cached.");
        int samples = 0;
        foreach (string id in ids)
        {
            Require(pcm.ContainsKey(id), "Missing " + id);
            float[] data = pcm[id];
            samples += data.Length;
            float peak = 0;
            foreach (float value in data)
            {
                Require(!float.IsNaN(value) && !float.IsInfinity(value), id + " has invalid PCM.");
                peak = Math.Max(peak, Math.Abs(value));
            }
            Require(peak > .01f && peak < .65f, id + " must be audible with mixing headroom.");
            Require(Math.Abs(data[0]) < .0001f, id + " begins with a click.");
            if (id != "ambient") Require(Math.Abs(data[data.Length - 1]) < .0001f, id + " ends with a click.");
        }
        var ambient = pcm["ambient"];
        float before = ambient[0] - ambient[ambient.Length - 1];
        float after = ambient[1] - ambient[0];
        Require(Math.Abs(before - after) < .00005f, "Ambient loop seam has an abrupt slope.");
        Require(pcm["ambient"].Length == 44100 * 4, "Ambient must contain 220 cycles of 55 Hz at 44.1 kHz.");
        Require(pcm["tap"].Length >= 44100 * .02f && pcm["tap"].Length <= 44100 * .04f, "Tap must stay short.");
        Require(pcm["complete"].Length >= 44100 * 2, "Complete should last two seconds.");
        var repeated = generate();
        foreach (string id in ids)
        {
            var a = pcm[id]; var b = repeated[id];
            Require(a.Length == b.Length, "Generation length changed.");
            for (int i = 0; i < a.Length; i++) Require(a[i] == b[i], "Generation must be deterministic.");
        }
        Require(coldMs < 150, "PCM generation exceeded 150 ms: " + coldMs.ToString("F1"));
        Console.WriteLine("PASS: 12 synthesized clips, finite bounded PCM, deterministic generation, short tap, seamless ambient, " + samples + " mono samples. Cold PCM generation " + coldMs.ToString("F1") + " ms (<150 ms). AudioClip upload and device playback require Unity runtime.");
        return 0;
    }
}
