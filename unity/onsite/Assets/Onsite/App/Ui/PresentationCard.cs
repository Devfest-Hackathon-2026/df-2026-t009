using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    // 発表用の全画面カード。地図の描画と、ツアー側の入力ロックはそのまま続ける。
    public sealed class PresentationCard : VisualElement
    {
        const float FadeSeconds = .40f;
        const float LineStagger = .30f;
        const float LineFadeSeconds = .30f;
        VisualElement currentFrame;

        public PresentationCard()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("tour-presentation-layer");
            style.display = DisplayStyle.None;
        }

        // 秒数には入場と本文の表示も含め、前のカードと重ねて切り替える。
        public IEnumerator Show(string kicker, string heading, float seconds, string[] lines, bool endCard = false)
        {
            lines = lines ?? Array.Empty<string>();
            bool entering = currentFrame == null;
            var previous = currentFrame;
            var next = new VisualElement { pickingMode = PickingMode.Ignore };
            next.AddToClassList("tour-presentation-frame");
            if (heading == "ONSITE") next.AddToClassList("tour-presentation-brand");
            if (endCard) next.AddToClassList("tour-presentation-end");
            next.style.opacity = 0;

            var copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.AddToClassList("tour-presentation-copy");
            var eyebrow = Text(kicker, "tour-presentation-kicker mono");
            if (string.IsNullOrEmpty(kicker)) eyebrow.style.display = DisplayStyle.None;
            copy.Add(eyebrow);
            copy.Add(Text(heading, "tour-presentation-heading"));
            var body = new VisualElement { pickingMode = PickingMode.Ignore };
            body.AddToClassList("tour-presentation-body");
            var labels = new Label[lines.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                // 終了カードのアドレスだけを欧文の等幅フォントにする。
                labels[i] = Text(lines[i], endCard && i == 1 ? "tour-presentation-line mono" : "tour-presentation-line");
                RevealLine(labels[i], 0);
                body.Add(labels[i]);
            }
            copy.Add(body);
            var band = new LaserBand(false, true);
            band.AddToClassList("tour-presentation-band");
            copy.Add(band);
            next.Add(copy); Add(next);
            currentFrame = next;
            style.display = DisplayStyle.Flex;
            style.opacity = entering ? 0 : 1;
            band.DrawIn();

            float started = Time.unscaledTime;
            float revealSeconds = FadeSeconds + (labels.Length == 0 ? 0 : (labels.Length - 1) * LineStagger + LineFadeSeconds);
            while (true)
            {
                float elapsed = Time.unscaledTime - started;
                float fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / FadeSeconds));
                if (entering) style.opacity = fade;
                next.style.opacity = fade;
                if (previous != null)
                {
                    previous.style.opacity = 1 - fade;
                    if (elapsed >= FadeSeconds) { previous.RemoveFromHierarchy(); previous = null; }
                }
                for (int i = 0; i < labels.Length; i++)
                {
                    float t = Mathf.Clamp01((elapsed - FadeSeconds - i * LineStagger) / LineFadeSeconds);
                    RevealLine(labels[i], 1 - Mathf.Pow(1 - t, 3));
                }
                if (elapsed >= revealSeconds) break;
                yield return null;
            }
            // 保持中はスタイルを書き換えず、最後のカードも表示したまま残す。
            while (Time.unscaledTime - started < seconds) yield return null;
        }

        public IEnumerator Hide()
        {
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < FadeSeconds)
            {
                style.opacity = 1 - Mathf.SmoothStep(0, 1, (Time.unscaledTime - started) / FadeSeconds);
                yield return null;
            }
            style.opacity = 0;
            style.display = DisplayStyle.None;
            Clear(); currentFrame = null;
        }

        static Label Text(string text, string classes)
        {
            var label = UiKit.Label(text, classes);
            label.pickingMode = PickingMode.Ignore;
            label.enableRichText = false;
            return label;
        }

        static void RevealLine(Label label, float opacity)
        {
            label.style.opacity = opacity;
            label.style.translate = new Translate(0, 10 * (1 - opacity), 0);
        }
    }
}