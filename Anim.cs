using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace PhotoStudio
{
    /// <summary>Движок плавных анимаций на базе Stopwatch и WinForms Timer.</summary>
    public static class Anim
    {
        public static float Ease(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            // Плавный старт и плавное торможение (ease-in-out cubic)
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
        }

        public static void Run(int ms, Action<float> step, Action done = null, bool ease = true, Control owner = null)
        {
            if (step == null) return;
            if (owner != null && (owner.IsDisposed || owner.Disposing)) return;

            int duration = Math.Max(16, ms);
            var sw = Stopwatch.StartNew();
            var timer = new System.Windows.Forms.Timer { Interval = 10 };
            bool finished = false;
            float lastApplied = -1f;

            void Cleanup(bool runDone)
            {
                if (finished) return;
                finished = true;
                timer.Stop();
                timer.Dispose();
                if (runDone && done != null && (owner == null || (!owner.IsDisposed && !owner.Disposing)))
                    done();
            }

            timer.Tick += (s, e) =>
            {
                if (owner != null && (owner.IsDisposed || owner.Disposing))
                {
                    Cleanup(false);
                    return;
                }

                float raw = (float)sw.Elapsed.TotalMilliseconds / duration;
                float p = Math.Clamp(raw, 0f, 1f);
                float val = ease ? Ease(p) : p;

                if (Math.Abs(val - lastApplied) >= 0.002f || raw >= 1f)
                {
                    lastApplied = val;
                    step(val);
                }

                if (raw >= 1f)
                    Cleanup(true);
            };

            if (owner != null)
                owner.Disposed += (s, e) => Cleanup(false);

            timer.Start();
        }

        /// <summary>Плавное появление окна.</summary>
        public static void FadeIn(Form f, int ms = 260)
        {
            if (f == null || f.IsDisposed) return;
            f.Opacity = 0.02;
            Run(ms, p =>
            {
                if (!f.IsDisposed)
                    f.Opacity = Math.Clamp(0.02 + 0.98 * p, 0.0, 1.0);
            }, null, true, f);
        }

        /// <summary>«Тряска» элемента при ошибке.</summary>
        public static void Shake(Control c)
        {
            if (c == null || c.IsDisposed) return;
            int baseX = c.Left;
            Run(380,
                p =>
                {
                    if (!c.IsDisposed)
                        c.Left = baseX + (int)Math.Round(Math.Sin(p * Math.PI * 6) * (1f - p) * 10f);
                },
                () =>
                {
                    if (!c.IsDisposed)
                        c.Left = baseX;
                },
                false,
                c);
        }
    }
}