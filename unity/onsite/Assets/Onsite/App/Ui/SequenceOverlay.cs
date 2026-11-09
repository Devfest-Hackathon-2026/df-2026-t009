using System;
using Onsite.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    public sealed class SequenceOverlay : VisualElement
    {
        readonly Label heading, subtitle, reward, kicker;
        IVisualElementScheduledItem animation;
        Action finished;
        float start, progress;
        bool complete;
        static readonly Color Cyan = new Color(0.498f, 0.890f, 1);
        static readonly Color Amber = new Color(1, 0.698f, 0.290f);
        static readonly Color Core = new Color(0.918f, 0.984f, 1);

        public SequenceOverlay()
        {
            AddToClassList("sequence-overlay");
            var copy = new VisualElement(); copy.AddToClassList("sequence-copy");
            kicker = UiKit.Label("FIELD / SIGNAL VERIFIED", "sequence-kicker mono");
            heading = UiKit.Label("UNLOCKED", "sequence-title");
            subtitle = UiKit.Label("ひらいた", "sequence-subtitle");
            reward = UiKit.Label("+30 EXP", "sequence-reward mono");
            copy.Add(kicker); copy.Add(heading); copy.Add(subtitle); copy.Add(reward); Add(copy);
            style.display = DisplayStyle.None;
            generateVisualContent += Paint;
            RegisterCallback<DetachFromPanelEvent>(_ => { animation?.Pause(); finished = null; });
        }

        public void Play(bool complete, Action done, string detail = null)
        {
            // 前の完了処理を取りこぼさない。
            var previous = finished; finished = null; animation?.Pause(); previous?.Invoke();
            this.complete = complete; finished = done;
            Sound.Play(complete ? "complete" : "unlock");
            DeviceBridge.Feedback(complete ? HapticKind.Complete : HapticKind.Unlock);
            start = Time.realtimeSinceStartup; progress = 0;
            heading.text = complete ? "ROUTE COMPLETE" : "UNLOCKED";
            kicker.text = complete ? "FIELD / ALL SIGNALS VERIFIED" : "FIELD / SIGNAL VERIFIED";
            subtitle.text = detail ?? (complete ? "巡りを達成した" : "この場所で、ひらいた");
            reward.text = complete ? "ROUTE / ALL SIGNALS OPEN" : "+30 EXP / DISCOVERY RECORDED";
            EnableInClassList("sequence-complete", complete);
            heading.style.opacity = subtitle.style.opacity = reward.style.opacity = kicker.style.opacity = 0;
            style.opacity = 0; style.display = DisplayStyle.Flex;
            animation = schedule.Execute(Tick).Every(16);
        }

        /// <summary>毎フレーム外から呼ぶ（UI の予約実行が止まっていても、演出が必ず進んで終わるように）。</summary>
        public void Pump()
        {
            if (finished != null) Tick();
        }

        void Tick()
        {
            if (progress >= 1 && finished == null) return;
            progress = Mathf.Clamp01((Time.realtimeSinceStartup - start) / 1.6f);
            float exit = Smooth(0.83f, 1, progress);
            style.opacity = Smooth(0, 0.08f, progress) * (1 - exit);
            Reveal(kicker, 0.20f, 0.32f);
            Reveal(heading, 0.25f, 0.40f);
            Reveal(subtitle, 0.31f, 0.45f);
            Reveal(reward, 0.38f, 0.52f);
            MarkDirtyRepaint();
            if (progress < 1) return;
            animation?.Pause(); style.display = DisplayStyle.None;
            var next = finished; finished = null; next?.Invoke();
        }

        void Reveal(VisualElement element, float from, float to)
        {
            float t = Smooth(from, to, progress);
            element.style.opacity = t;
            element.style.translate = new Translate(0, 10 * (1 - t), 0);
        }

        static float Smooth(float from, float to, float value)
        {
            float t = Mathf.InverseLerp(from, to, value);
            return 1 - Mathf.Pow(1 - t, 3);
        }

        void Paint(MeshGenerationContext context)
        {
            var p = context.painter2D;
            float w = contentRect.width, h = contentRect.height;
            if (w <= 0 || h <= 0) return;
            Color accent = complete ? Amber : Cyan;
            var center = new Vector2(w * 0.5f, h * 0.35f);
            float radius = Mathf.Min(w * 0.125f, Mathf.Min(h * 0.13f, 122));
            float extend = Smooth(0, 0.25f, progress) * (1 - Smooth(0.83f, 1, progress));
            float left = w * 0.08f, right = w * 0.92f;
            float top = center.y - radius * 1.32f, bottom = center.y + radius * 1.32f;
            Laser(p, new Vector2(left, top), new Vector2(Mathf.Lerp(left, right, extend), top), accent, 0.65f);
            Laser(p, new Vector2(right, bottom), new Vector2(Mathf.Lerp(right, left, extend), bottom), accent, 0.65f);

            // 六辺を順に描き、最後に巻き戻す。
            float edges = Smooth(0.07f, 0.39f, progress) * 6;
            edges *= 1 - Smooth(0.82f, 0.98f, progress);
            for (int i = 0; i < 6; i++)
            {
                float part = Mathf.Clamp01(edges - i);
                if (part <= 0) continue;
                float a = -Mathf.PI / 2 + i * Mathf.PI / 3, b = a + Mathf.PI / 3;
                var from = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                var to = center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
                Laser(p, from, Vector2.Lerp(from, to, part), accent, 0.92f);
            }
            float mark = Smooth(0.30f, 0.44f, progress) * (1 - Smooth(0.80f, 0.96f, progress));
            if (mark > 0)
            {
                var a = center + new Vector2(-radius * 0.33f, 0);
                var b = center + new Vector2(-radius * 0.08f, radius * 0.23f);
                var c = center + new Vector2(radius * 0.38f, -radius * 0.27f);
                Laser(p, a, Vector2.Lerp(a, b, Mathf.Clamp01(mark * 2)), accent, mark);
                if (mark > 0.5f) Laser(p, b, Vector2.Lerp(b, c, (mark - 0.5f) * 2), accent, mark);
            }

            float sweep = Mathf.InverseLerp(0.11f, 0.46f, progress);
            if (sweep > 0 && sweep < 1)
            {
                float x = Mathf.Lerp(-w * 0.18f, w * 1.18f, sweep);
                Laser(p, new Vector2(x - w * 0.16f, center.y), new Vector2(x, center.y), Core, 0.85f * Mathf.Sin(sweep * Mathf.PI));
            }
            float flash = Mathf.Clamp01(1 - Mathf.Abs(progress - 0.34f) / 0.045f);
            if (flash > 0)
            {
                p.fillColor = new Color(Core.r, Core.g, Core.b, flash * 0.10f);
                Draw.Box(p, new Rect(0, top, w, bottom - top), true);
            }
        }

        static void Laser(Painter2D p, Vector2 from, Vector2 to, Color accent, float alpha)
        {
            for (int layer = 3; layer >= 0; layer--)
            {
                p.lineWidth = layer == 0 ? 1 : layer * 3;
                var color = layer == 0 ? Core : accent;
                color.a = alpha * (layer == 0 ? 0.95f : layer == 1 ? 0.18f : layer == 2 ? 0.07f : 0.025f);
                p.strokeColor = color; Draw.Line(p, from, to);
            }
        }
    }
}
