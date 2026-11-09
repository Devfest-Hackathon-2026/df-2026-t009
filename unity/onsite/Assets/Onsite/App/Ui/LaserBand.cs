using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    // 光の芯と余光を重ねる。常時更新はしない。
    public sealed class LaserBand : VisualElement
    {
        readonly IVisualElementScheduledItem frame, scan;
        float started, sweepStarted = -100, flashStarted = -100, reveal = 1;
        bool drawing, amber;
        public bool Amber { get => amber; set { amber = value; MarkDirtyRepaint(); } }
        public bool Centered { get; set; }
        public bool Scan { get; set; }
        public LaserBand(bool amber = false, bool centered = false)
        {
            this.amber = amber; Centered = centered;
            pickingMode = PickingMode.Ignore; AddToClassList("laser-band");
            generateVisualContent += Paint;
            frame = schedule.Execute(Tick).Every(16); frame.Pause();
            scan = schedule.Execute(() =>
            {
                if (!Scan || Centered) return;
                for (var element = (VisualElement)this; element != null; element = element.parent)
                    if (!element.visible || element.resolvedStyle.display == DisplayStyle.None || element.resolvedStyle.opacity <= 0) return;
                sweepStarted = Time.unscaledTime; frame.Resume();
            }).Every(6800); scan.Pause();
            RegisterCallback<AttachToPanelEvent>(_ => { DrawIn(); if (Scan && !Centered) scan.Resume(); });
            RegisterCallback<DetachFromPanelEvent>(_ => { frame.Pause(); scan.Pause(); });
        }
        public void DrawIn()
        {
            started = Time.unscaledTime; reveal = 0; drawing = true;
            frame.Resume(); MarkDirtyRepaint();
        }
        public void Flash()
        {
            flashStarted = Time.unscaledTime; frame.Resume(); MarkDirtyRepaint();
        }
        void Tick()
        {
            float now = Time.unscaledTime;
            if (drawing)
            {
                float t = Mathf.Clamp01((now - started) / .30f);
                reveal = 1 - Mathf.Pow(1 - t, 3); drawing = t < 1;
            }
            MarkDirtyRepaint();
            if (!drawing && now - sweepStarted > .65f && now - flashStarted > .22f) frame.Pause();
        }
        void Paint(MeshGenerationContext context)
        {
            float width = contentRect.width * reveal, height = contentRect.height;
            if (width <= 0 || height <= 0) return;
            var p = context.painter2D;
            var tint = Amber ? new Color(1, .698f, .29f) : new Color(.498f, .89f, 1);
            float flash = Mathf.Clamp01(1 - (Time.unscaledTime - flashStarted) / .22f);
            float sweep = (Time.unscaledTime - sweepStarted) / .65f;
            float y = height * .55f;
            const int pieces = 48;
            for (int i = 0; i < pieces; i++)
            {
                float u = (i + .5f) / pieces;
                float fade = Centered ? Mathf.Sin(u * Mathf.PI) : Mathf.Pow(1 - u, 1.2f);
                float shine = sweep >= 0 && sweep <= 1 ? Mathf.Max(0, 1 - Mathf.Abs(u - sweep) / .09f) : 0;
                float x0 = width * i / pieces, x1 = width * (i + 1) / pieces;
                p.fillColor = new Color(tint.r, tint.g, tint.b, fade * (.055f + flash * .06f));
                Draw.Box(p, new Rect(x0, 0, x1 - x0 + .3f, height), true);
                for (int layer = 0; layer < 3; layer++)
                {
                    p.lineWidth = layer == 0 ? 8 : layer == 1 ? 3 : 1;
                    float alpha = layer == 0 ? .055f : layer == 1 ? .16f : .8f;
                    var color = Color.Lerp(tint, new Color(.918f, .984f, 1), layer == 2 ? .8f : 0);
                    color.a = fade * Mathf.Clamp01(alpha + shine * .45f + flash * .3f);
                    p.strokeColor = color;
                    Draw.Line(p, new Vector2(x0, y), new Vector2(x1 + .4f, y));
                }
            }
        }
    }

    // リストの背面だけに、端へ消える濃淡を敷く。
    public sealed class FadeSurface : VisualElement
    {
        static Texture2D horizontal, vertical;
        public FadeSurface(bool bottom = false)
        {
            pickingMode = PickingMode.Ignore; AddToClassList("fade-surface");
            style.backgroundImage = GetTexture(bottom);
        }
        static Texture2D GetTexture(bool bottom)
        {
            var cached = bottom ? vertical : horizontal;
            if (cached != null) return cached;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = bottom ? "Onsite gradient vertical" : "Onsite gradient horizontal",
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1), v = y / (float)(size - 1);
                    float alpha = bottom ? .82f * Mathf.SmoothStep(0, 1, 1 - v) : .82f * Mathf.Pow(1 - u, .55f);
                    pixels[y * size + x] = new Color(4 / 255f, 10 / 255f, 16 / 255f, alpha);
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            if (bottom) vertical = texture; else horizontal = texture;
            return texture;
        }
    }
}
