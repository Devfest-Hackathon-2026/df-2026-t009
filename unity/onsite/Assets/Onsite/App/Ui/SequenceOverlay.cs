using System;
using Onsite.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App.Ui
{
    public sealed class SequenceOverlay : VisualElement
    {
        readonly Label heading;
        readonly Label subtitle;
        readonly Label reward;
        IVisualElementScheduledItem animation;
        Action finished;
        float start;
        float progress;
        public SequenceOverlay()
        {
            AddToClassList("sequence-overlay");
            heading = UiKit.Label("UNLOCKED", "sequence-title"); subtitle = UiKit.Label("ひらいた", "sequence-subtitle");
            reward = UiKit.Label("+30 EXP", "sequence-reward mono"); Add(heading); Add(subtitle); Add(reward);
            style.display = DisplayStyle.None;
            generateVisualContent += Paint;
        }

        public void Play(bool complete, Action done, string detail = null)
        {
            // 演出が終わってから本文を出し、初開封の瞬間を一つにまとめる。
            animation?.Pause(); finished = done; start = Time.realtimeSinceStartup; progress = 0;
            heading.text = complete ? "COMPLETE" : "UNLOCKED";
            subtitle.text = detail ?? (complete ? "巡りを達成した" : "ひらいた"); reward.text = complete ? "ROUTE / ALL SIGNALS OPEN" : "+30 EXP";
            style.display = DisplayStyle.Flex;
            animation = schedule.Execute(Tick).Every(16);
        }
        void Tick()
        {
            progress = Mathf.Clamp01((Time.realtimeSinceStartup - start) / 1.6f); MarkDirtyRepaint();
            if (progress < 1) return;
            animation.Pause(); style.display = DisplayStyle.None;
            var next = finished; finished = null; next?.Invoke();
        }
        void Paint(MeshGenerationContext context)
        {
            var p = context.painter2D; var w = contentRect.width; var h = contentRect.height;
            p.strokeColor = new Color(1,0.7f,0.29f,0.08f); p.lineWidth = 1;
            for (float y = 0; y < h; y += 7) Draw.Line(p,new Vector2(0,y),new Vector2(w,y));
            p.strokeColor = new Color(1,0.7f,0.29f,0.8f); p.lineWidth = 2;
            var sweep = h * progress; Draw.Line(p,new Vector2(0,sweep),new Vector2(w,sweep));
            var center = new Vector2(w/2,h*0.39f); var radius = Mathf.Min(w*0.2f,165);
            var edges = Mathf.Clamp(Mathf.CeilToInt(progress * 16),1,6);
            for (var i = 0; i < edges; i++)
            {
                var a = Mathf.PI/6 + i*Mathf.PI/3; var b = a+Mathf.PI/3;
                Draw.Line(p,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius);
            }
            p.fillColor = MarkerField.Amber;
            for (var i = 0; i < 34; i++)
            {
                var a = i * 2.39996f; var r = radius + progress * (50 + i*8);
                var c = center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;
                var alpha = Mathf.Sin(progress*Mathf.PI)*(0.3f+0.7f*(i%4)/3f);
                p.fillColor = new Color(1,0.7f,0.29f,alpha);
                Draw.Box(p,new Rect(c.x,c.y,2+i%3,2+i%3),true);
            }
        }
    }
}
