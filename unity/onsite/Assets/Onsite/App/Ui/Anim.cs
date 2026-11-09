using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    public static class Anim
    {
        sealed class Motion
        {
            public IVisualElementScheduledItem Enter, Draw, Flash;
            public Motion() { }
        }
        static readonly ConditionalWeakTable<VisualElement, Motion> motions = new ConditionalWeakTable<VisualElement, Motion>();

        public static void Enter(VisualElement element, int delayMs = 0)
        {
            if (element == null) return;
            Reveal(element, delayMs);
            element.Query<LaserBand>().ForEach(band => band.DrawIn());
            // 子の表示を少しずらす。
            int stagger = 0;
            foreach (var child in element.Children())
            {
                if (stagger >= 8 || child.style.position.value == Position.Absolute) continue;
                Reveal(child, delayMs + 30 * ++stagger);
            }
        }

        static void Reveal(VisualElement element, int delayMs)
        {
            var motion = motions.GetOrCreateValue(element);
            motion.Enter?.Pause();
            element.style.opacity = 0;
            element.style.translate = new Translate(0, 12, 0);
            float start = Time.realtimeSinceStartup + delayMs / 1000f;
            motion.Enter = element.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / 0.21f);
                float eased = 1 - Mathf.Pow(1 - t, 3);
                element.style.opacity = eased;
                element.style.translate = new Translate(0, 12 * (1 - eased), 0);
                if (t >= 1)
                {
                    motion.Enter.Pause();
                    element.style.opacity = StyleKeyword.Null;
                    element.style.translate = StyleKeyword.Null;
                }
            }).Every(16);
            motion.Enter.StartingIn(delayMs);
        }

        public static void Draw(VisualElement element, int delayMs = 0)
        {
            if (element == null) return;
            var motion = motions.GetOrCreateValue(element);
            motion.Draw?.Pause();
            element.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50), 0);
            element.style.scale = new Scale(new Vector3(0, 1, 1));
            float start = Time.realtimeSinceStartup + delayMs / 1000f;
            motion.Draw = element.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / 0.28f);
                element.style.scale = new Scale(new Vector3(1 - Mathf.Pow(1 - t, 3), 1, 1));
                if (t >= 1)
                {
                    motion.Draw.Pause();
                    element.style.scale = StyleKeyword.Null;
                }
            }).Every(16);
            motion.Draw.StartingIn(delayMs);
        }

        public static void Flash(VisualElement element)
        {
            if (element == null) return;
            var motion = motions.GetOrCreateValue(element);
            motion.Flash?.Pause();
            element.AddToClassList("laser-flash");
            motion.Flash = element.schedule.Execute(() => element.RemoveFromClassList("laser-flash"));
            motion.Flash.StartingIn(150);
        }
    }
}
