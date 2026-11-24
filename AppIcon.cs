using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PhotoStudio
{
    /// <summary>Генерация иконки приложения в стиле объектива фотокамеры.</summary>
    public static class AppIcon
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyIcon(IntPtr hIcon);

        public static Icon Create(int size = 64)
        {
            using var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            float pad = size * 0.06f;
            float w = size - 2f * pad;
            var outerRect = new RectangleF(pad, pad, w, w);

            using (var path = CreateRoundedRect(outerRect, size * 0.24f))
            using (var bodyBrush = new LinearGradientBrush(
                outerRect,
                Color.FromArgb(44, 47, 61),
                Color.FromArgb(20, 21, 28),
                LinearGradientMode.ForwardDiagonal))
            {
                g.FillPath(bodyBrush, path);
            }

            float ringPad = size * 0.16f;
            var ringRect = new RectangleF(ringPad, ringPad, size - 2f * ringPad, size - 2f * ringPad);
            using (var ringPen = new Pen(Theme.Accent, Math.Max(2f, size * 0.065f)))
            {
                g.DrawEllipse(ringPen, ringRect);
            }

            float lensPad = size * 0.27f;
            var lensRect = new RectangleF(lensPad, lensPad, size - 2f * lensPad, size - 2f * lensPad);
            using (var lensBrush = new LinearGradientBrush(
                lensRect,
                Color.FromArgb(30, 58, 74),
                Color.FromArgb(14, 16, 22),
                LinearGradientMode.ForwardDiagonal))
            {
                g.FillEllipse(lensBrush, lensRect);
            }

            var flareRect = new RectangleF(size * 0.34f, size * 0.32f, size * 0.14f, size * 0.14f);
            using (var flareBrush = new SolidBrush(Color.FromArgb(210, Theme.Teal)))
            {
                g.FillEllipse(flareBrush, flareRect);
            }

            IntPtr hIcon = bmp.GetHicon();
            try
            {
                using var temp = Icon.FromHandle(hIcon);
                return (Icon)temp.Clone();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        public static void Apply(Form form)
        {
            if (form == null || form.IsDisposed) return;
            var old = form.Icon;
            form.Icon = Create(64);
            if (old != null) old.Dispose();
        }

        static GraphicsPath CreateRoundedRect(RectangleF r, float radius)
        {
            float d = radius * 2f;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180f, 90f);
            path.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            path.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }
    }
}