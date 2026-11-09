using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    // 撮影中の字幕。地図と画面の操作を遮らない。
    public sealed class Caption : VisualElement
    {
        readonly VisualElement copy, endCard;
        readonly Label mainLine, smallLine;
        readonly LaserBand band;
        IVisualElementScheduledItem motion, endMotion;
        float opacity;

        public Caption()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("tour-caption-layer");
            copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.AddToClassList("tour-caption");
            mainLine = UiKit.Label("", "tour-caption-main");
            smallLine = UiKit.Label("", "tour-caption-small mono");
            mainLine.pickingMode = smallLine.pickingMode = PickingMode.Ignore;
            band = new LaserBand(false, true);
            band.AddToClassList("tour-caption-band");
            copy.Add(mainLine); copy.Add(smallLine); copy.Add(band); Add(copy);
            SetOpacity(0);

            endCard = new VisualElement { pickingMode = PickingMode.Ignore };
            endCard.AddToClassList("tour-end-card");
            var title = UiKit.Label("ONSITE", "tour-end-title");
            var tagline = UiKit.Label("近づくと、ひらく。", "tour-end-tagline");
            var address = UiKit.Label("kokodake-t009.web.app", "tour-end-address mono");
            title.pickingMode = tagline.pickingMode = address.pickingMode = PickingMode.Ignore;
            endCard.Add(title); endCard.Add(tagline); endCard.Add(address); Add(endCard);
            endCard.style.display = DisplayStyle.None;
            RegisterCallback<DetachFromPanelEvent>(_ => { motion?.Pause(); endMotion?.Pause(); });
        }

        public void Show(string main, string small = null)
        {
            mainLine.text = main ?? "";
            smallLine.text = small ?? "";
            smallLine.style.display = string.IsNullOrEmpty(small) ? DisplayStyle.None : DisplayStyle.Flex;
            motion?.Pause();
            SetOpacity(0);
            Animate(1, .30f);
            band.DrawIn();
        }

        public void Hide() => Animate(0, .25f);

        void SetOpacity(float value)
        {
            opacity = value;
            copy.style.opacity = value;
            copy.style.translate = new Translate(0, 12 * (1 - value), 0);
        }

        void Animate(float target, float seconds)
        {
            motion?.Pause();
            float from = opacity, started = Time.realtimeSinceStartup;
            motion = schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - started) / seconds);
                SetOpacity(Mathf.Lerp(from, target, 1 - Mathf.Pow(1 - t, 3)));
                if (t >= 1) motion.Pause();
            }).Every(16);
        }

        public void ShowEndCard()
        {
            Hide(); endMotion?.Pause();
            endCard.style.opacity = 0;
            endCard.style.display = DisplayStyle.Flex;
            float started = Time.realtimeSinceStartup;
            endMotion = schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - started) / .90f);
                endCard.style.opacity = Mathf.SmoothStep(0, 1, t);
                if (t >= 1) endMotion.Pause();
            }).Every(16);
        }
    }
}
