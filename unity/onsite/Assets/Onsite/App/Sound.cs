using System;
using System.Collections.Generic;
using UnityEngine;

namespace Onsite.App
{
    public sealed class Sound : MonoBehaviour
    {
        const int SampleRate = 44100, VoiceCount = 6;
        const float RepeatDelay = .06f;
        static Sound instance;
        static bool quitting;
        static float masterVolume = 1f;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        readonly AudioSource[] voices = new AudioSource[VoiceCount];
        AudioSource ambient, verify;
        bool muted, gesture, suspended, ambientWanted, verifyWanted, mapActive = true, pendingTap;
        int nextVoice, tapFrame, semanticFrame = -1;
        float ambientLevel;

#if UNITY_WEBGL && !UNITY_EDITOR
        // ブラウザー版はスレッドを使うため、Unity の AudioClip に後から波形を入れられない（共有メモリをブラウザーが拒む）。
        // 波形はブラウザーの WebAudio に渡し、そちらで鳴らす。
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void Onsite_AudioRegister(string id, float[] data, int length, int rate);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int Onsite_AudioPlay(string id, float volume, bool loop);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void Onsite_AudioVolume(int handle, float volume);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void Onsite_AudioStop(int handle);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void Onsite_AudioStopAll();
        static readonly bool Web = true;
#else
        static void Onsite_AudioRegister(string id, float[] data, int length, int rate) { }
        static int Onsite_AudioPlay(string id, float volume, bool loop) => -1;
        static void Onsite_AudioVolume(int handle, float volume) { }
        static void Onsite_AudioStop(int handle) { }
        static void Onsite_AudioStopAll() { }
        static readonly bool Web = false;
#endif
        int ambientHandle = -1, verifyHandle = -1;

        public static float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = float.IsNaN(value) ? 0 : Mathf.Clamp01(value);
                if (instance != null) instance.ApplyVolume();
            }
        }

        public static bool Muted
        {
            get { Initialize(); return instance != null && instance.muted; }
            set
            {
                Initialize();
                if (instance == null) return;
                instance.muted = value;
                PlayerPrefs.SetInt("onsite.sound", value ? 0 : 1);
                PlayerPrefs.Save();
                if (value) instance.Silence();
                else instance.SyncLoops();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; quitting = false; masterVolume = 1f; }

        public static void Initialize()
        {
            if (instance != null || quitting) return;
            var host = new GameObject("OnsiteSound");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<Sound>();
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            muted = PlayerPrefs.GetInt("onsite.sound", 1) == 0;
            // 音を持たない既存シーンにも受け手を用意する。
            if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            if (Web)
            {
                foreach (var entry in GeneratePcm()) Onsite_AudioRegister(entry.Key, entry.Value, entry.Value.Length, SampleRate);
                return;
            }
            foreach (var entry in GeneratePcm())
            {
                var clip = AudioClip.Create("onsite." + entry.Key, entry.Value.Length, 1, SampleRate, false);
                clip.SetData(entry.Value, 0);
                clips.Add(entry.Key, clip);
            }
            for (int i = 0; i < voices.Length; i++) voices[i] = NewSource(false);
            ambient = NewSource(true); ambient.clip = clips["ambient"];
            verify = NewSource(true); verify.clip = clips["verify"];
            ApplyVolume();
        }

        AudioSource NewSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0;
            source.priority = loop ? 160 : 80;
            return source;
        }

        // ブラウザーの音は、最初の操作まで開始しない。
        public static void UserGesture()
        {
            Initialize();
            if (instance == null || instance.gesture) return;
            instance.gesture = true;
            instance.SyncLoops();
        }

        public static void ButtonPress(Action clicked)
        {
            UserGesture();
            try { clicked?.Invoke(); }
            finally { Tap(); }
        }

        // 同じ操作の選択音などを優先する。
        public static void SuppressTap()
        {
            Initialize();
            if (instance != null) instance.semanticFrame = Time.frameCount;
        }

        public static void Tap()
        {
            Initialize();
            if (instance == null) return;
            instance.pendingTap = true;
            instance.tapFrame = Time.frameCount;
        }

        public static void Play(string id)
        {
            Initialize();
            if (instance == null || string.IsNullOrEmpty(id)) return;
            if (id == "ambient" || id == "verify") { StartLoop(id); return; }
            if (id != "tap") instance.semanticFrame = Time.frameCount;
            instance.PlayOneShot(id);
        }

        void PlayOneShot(string id)
        {
            AudioClip clip = null;
            if (!CanPlay || (!Web && !clips.TryGetValue(id, out clip))) return;
            float now = Time.realtimeSinceStartup;
            if (lastPlayed.TryGetValue(id, out var previous) && now - previous < RepeatDelay) return;
            lastPlayed[id] = now;
            if (Web) { Onsite_AudioPlay(id, masterVolume * .7f, false); return; }
            AudioSource source = null;
            for (int i = 0; i < voices.Length; i++)
                if (!voices[i].isPlaying) { source = voices[i]; break; }
            if (source == null) source = voices[nextVoice++ % voices.Length];
            source.Stop(); source.clip = clip; source.volume = masterVolume * .7f; source.Play();
        }

        public static void StartLoop(string id)
        {
            Initialize();
            if (instance == null) return;
            if (id == "ambient") instance.ambientWanted = true;
            else if (id == "verify")
            {
                instance.semanticFrame = Time.frameCount;
                instance.verifyWanted = true;
            }
            else return;
            instance.SyncLoops();
        }

        public static void StopLoop(string id)
        {
            if (instance == null) return;
            if (id == "ambient") { instance.ambientWanted = false; instance.StopAmbient(); instance.ambientLevel = 0; }
            else if (id == "verify") { instance.verifyWanted = false; instance.StopVerify(); }
        }

        public static void SetMapActive(bool active)
        {
            Initialize();
            if (instance == null) return;
            instance.mapActive = active;
            instance.ambientWanted = true;
            instance.SyncLoops();
        }

        bool CanPlay => gesture && !muted && !suspended && isActiveAndEnabled;

        void SyncLoops()
        {
            if (!CanPlay) return;
            if (Web)
            {
                if (ambientWanted && ambientHandle < 0) ambientHandle = Onsite_AudioPlay("ambient", 0, true);
                if (verifyWanted && verifyHandle < 0) verifyHandle = Onsite_AudioPlay("verify", .18f * masterVolume, true);
                ApplyVolume();
                return;
            }
            if (ambientWanted && !ambient.isPlaying) ambient.Play();
            if (verifyWanted && !verify.isPlaying) verify.Play();
            ApplyVolume();
        }

        void StopAmbient()
        {
            if (Web) { if (ambientHandle >= 0) Onsite_AudioStop(ambientHandle); ambientHandle = -1; }
            else if (ambient != null) ambient.Stop();
        }
        void StopVerify()
        {
            if (Web) { if (verifyHandle >= 0) Onsite_AudioStop(verifyHandle); verifyHandle = -1; }
            else if (verify != null) verify.Stop();
        }

        void ApplyVolume()
        {
            if (Web)
            {
                if (ambientHandle >= 0) Onsite_AudioVolume(ambientHandle, muted ? 0 : ambientLevel * masterVolume);
                if (verifyHandle >= 0) Onsite_AudioVolume(verifyHandle, muted ? 0 : .18f * masterVolume);
                return;
            }
            foreach (var source in voices) if (source != null) source.volume = muted ? 0 : masterVolume * .7f;
            if (ambient != null) ambient.volume = muted ? 0 : ambientLevel * masterVolume;
            if (verify != null) verify.volume = muted ? 0 : .18f * masterVolume;
        }

        void Update()
        {
            float target = CanPlay && ambientWanted ? (mapActive ? .06f : .02f) : 0;
            ambientLevel = Mathf.MoveTowards(ambientLevel, target, Time.unscaledDeltaTime * .12f);
            ApplyVolume();
        }

        void LateUpdate()
        {
            if (!pendingTap) return;
            pendingTap = false;
            if (semanticFrame < tapFrame) PlayOneShot("tap");
        }

        void Silence()
        {
            pendingTap = false; ambientLevel = 0;
            foreach (var source in voices) if (source != null) source.Stop();
            if (Web) { Onsite_AudioStopAll(); ambientHandle = verifyHandle = -1; }
            if (ambient != null) ambient.Stop();
            if (verify != null) verify.Stop();
            ApplyVolume();
        }

        void OnApplicationPause(bool pause)
        {
            suspended = pause;
            if (pause) Silence(); else SyncLoops();
        }

        void OnDisable() => Silence();
        void OnEnable() { if (ambient != null || Web) SyncLoops(); }
        void OnApplicationQuit() { quitting = true; }
        void OnDestroy()
        {
            foreach (var clip in clips.Values) Destroy(clip);
            clips.Clear();
            if (instance == this) instance = null;
        }

        // 三角関数は小さな表を共有し、生成時の負荷を抑える。
        static Dictionary<string, float[]> GeneratePcm()
        {
            var pcm = new Dictionary<string, float[]>();
            var synth = new Synth();
            float[] Make(string id, float seconds) { var data = new float[(int)(SampleRate * seconds)]; pcm.Add(id, data); return data; }
            var tap = Make("tap", .032f);
            synth.Tone(tap, 0, .03f, 2100, 2050, .12f, .001f);
            synth.Noise(tap, 0, .009f, .09f, .65f, false);
            var select = Make("select", .22f);
            synth.Tone(select, 0, .12f, 870, 1040, .19f, .003f);
            synth.Tone(select, .075f, .14f, 1160, 1380, .17f, .003f);
            synth.Tone(select, .075f, .14f, 1164, 1384, .04f, .004f);
            synth.Noise(Make("open_panel", .16f), 0, .16f, .16f, .07f, true);
            synth.Noise(Make("close_panel", .15f), 0, .15f, .13f, .07f, false);
            var arrival = Make("arrival", .9f);
            synth.Tone(arrival, 0, .52f, 1200, 1200, .24f, .004f);
            synth.Tone(arrival, .2f, .47f, 1200, 1200, .055f, .005f);
            synth.Tone(arrival, .47f, .4f, 110, 95, .19f, .02f);
            var scan = Make("verify", .72f);
            for (int i = 0; i < 3; i++)
            {
                synth.Tone(scan, i * .24f, .045f, 880 + i * 70, 900 + i * 70, .11f, .002f);
                synth.Noise(scan, i * .24f, .014f, .035f, .3f, false);
            }
            var unlock = Make("unlock", 1.55f);
            synth.Sweep(unlock, .38f);
            synth.Tone(unlock, .24f, 1.3f, 523.251f, 523.251f, .16f, .065f);
            synth.Tone(unlock, .24f, 1.3f, 659.255f, 659.255f, .12f, .065f);
            synth.Tone(unlock, .24f, 1.3f, 783.991f, 783.991f, .1f, .065f);
            var complete = Make("complete", 2.05f);
            synth.Tone(complete, 0, 1.18f, 261.626f, 261.626f, .18f, .07f);
            synth.Tone(complete, 0, 1.18f, 329.628f, 329.628f, .12f, .07f);
            synth.Tone(complete, 0, 1.18f, 391.995f, 391.995f, .1f, .07f);
            synth.Tone(complete, .72f, 1.32f, 349.228f, 349.228f, .16f, .07f);
            synth.Tone(complete, .72f, 1.32f, 440, 440, .11f, .07f);
            synth.Tone(complete, .72f, 1.32f, 523.251f, 523.251f, .09f, .07f);
            var like = Make("like", .21f);
            synth.Tone(like, 0, .2f, 1760, 1810, .16f, .003f);
            synth.Tone(like, .035f, .17f, 2640, 2660, .04f, .004f);
            var send = Make("send", .2f);
            synth.Noise(send, 0, .15f, .14f, .11f, true);
            synth.Tone(send, .04f, .13f, 620, 1500, .12f, .008f);
            synth.Tone(send, .15f, .04f, 1700, 1700, .09f, .001f);
            var error = Make("error", .31f);
            for (int i = 0; i < 2; i++)
            {
                synth.Tone(error, i * .16f, .14f, 135, 122, .18f, .012f);
                synth.Tone(error, i * .16f, .14f, 181, 164, .055f, .012f);
            }
            synth.Ambient(Make("ambient", 4));
            return pcm;
        }

        sealed class Synth
        {
            const int TableSize = 4096;
            readonly float[] sine = new float[TableSize];
            uint seed = 0x37ae29bd;
            public Synth() { for (int i = 0; i < TableSize; i++) sine[i] = (float)Math.Sin(i * (Math.PI * 2 / TableSize)); }
            float Sin(double phase)
            {
                double index = (phase - Math.Floor(phase)) * TableSize;
                int lo = (int)index;
                return sine[lo] + (sine[(lo + 1) & (TableSize - 1)] - sine[lo]) * (float)(index - lo);
            }
            float Random()
            {
                seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                return (seed & 0xffffff) / 8388608f - 1;
            }

            public void Tone(float[] data, float delay, float duration, float from, float to, float gain, float attack)
            {
                int start = (int)(delay * SampleRate), length = Math.Min((int)(duration * SampleRate), data.Length - start);
                double phase = 0, step = from / SampleRate, delta = (to - from) / (SampleRate * (double)length);
                float decay = 1, fall = (float)Math.Exp(-6.5 / length), rise = 1 / Math.Max(1, attack * SampleRate);
                for (int i = 0; i < length; i++)
                {
                    float edge = Math.Min(1, (length - 1 - i) / 128f);
                    data[start + i] += Sin(phase) * gain * decay * Math.Min(1, i * rise) * edge;
                    phase += step; step += delta; decay *= fall;
                }
            }

            public void Noise(float[] data, float delay, float duration, float gain, float filter, bool up)
            {
                int start = (int)(delay * SampleRate), length = Math.Min((int)(duration * SampleRate), data.Length - start);
                float low = 0;
                for (int i = 0; i < length; i++)
                {
                    float t = i / (float)(length - 1);
                    low += (Random() - low) * (filter * (up ? .3f + t : 1.3f - t));
                    float envelope = 4 * t * (1 - t);
                    data[start + i] += low * gain * envelope * envelope;
                }
            }

            public void Sweep(float[] data, float duration)
            {
                int length = (int)(duration * SampleRate);
                double carrier = 0, modulator = 0;
                for (int i = 0; i < length; i++)
                {
                    float t = i / (float)(length - 1), envelope = 4 * t * (1 - t);
                    data[i] += Sin(carrier + .18 * (1 - t) * Sin(modulator)) * .22f * envelope * envelope;
                    carrier += (300 + 1700 * t * t) / SampleRate;
                    modulator += (120 + 160 * t) / SampleRate;
                }
            }

            public void Ambient(float[] data)
            {
                float low = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    low += (Random() - low) * .006f;
                    float t = i / (float)data.Length;
                    float lfo = .72f + .28f * Sin(t);
                    data[i] = .19f * Sin(220 * t) * lfo + low * .5f;
                }
                // 雑音の継ぎ目も、周期の両端へ静かに戻す。
                int fade = SampleRate / 4;
                for (int i = 0; i < fade; i++)
                {
                    float t = i / (float)fade;
                    float window = .5f - .5f * Sin(.25 + t * .5);
                    int end = data.Length - 1 - i;
                    float frontTone = .19f * Sin(220 * (i / (double)data.Length)) * (.72f + .28f * Sin(i / (double)data.Length));
                    float endTone = .19f * Sin(220 * (end / (double)data.Length)) * (.72f + .28f * Sin(end / (double)data.Length));
                    data[i] = frontTone + (data[i] - frontTone) * window;
                    data[end] = endTone + (data[end] - endTone) * window;
                }
            }
        }
    }
}
