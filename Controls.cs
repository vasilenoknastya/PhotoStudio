using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace PhotoStudio
{
    public enum BtnStyle { Primary, Secondary, Danger, Ghost }

    /// <summary>Кнопка с анимацией: плавная смена цвета при наведении и «вдавливание» при нажатии.</summary>
    public class AnimButton : Button
    {
        readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer { Interval = 15 };
        float _hover;
        bool _over, _down;

        public Color BaseColor { get; set; }
        public Color HoverColor { get; set; }
        public Color PressColor { get; set; }
        public Color TextColor { get; set; }
        public int Radius { get; set; } = 8;

        public AnimButton(string text, int width = 110, BtnStyle style = BtnStyle.Primary)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = text;
            Size = new Size(width, 38);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = Theme.BodyBold;
            UseVisualStyleBackColor = false;
            ApplyStyle(style);

            _timer.Tick += (s, e) =>
            {
                if (IsDisposed || Disposing) { _timer.Stop(); return; }
                float target = _over ? 1f : 0f;
                _hover += (target - _hover) * 0.28f;
                if (Math.Abs(target - _hover) < 0.02f) { _hover = target; _timer.Stop(); }
                Invalidate();
            };
        }

        public void ApplyStyle(BtnStyle s)
        {
            switch (s)
            {
                case BtnStyle.Primary:
                    BaseColor = Theme.Accent; HoverColor = Theme.AccentHover; PressColor = Theme.AccentPress;
                    TextColor = Color.FromArgb(30, 24, 12);
                    TextAlign = ContentAlignment.MiddleCenter;
                    break;
                case BtnStyle.Secondary:
                    BaseColor = Theme.Surface2; HoverColor = Color.FromArgb(58, 62, 78); PressColor = Color.FromArgb(35, 37, 47);
                    TextColor = Theme.Fg;
                    TextAlign = ContentAlignment.MiddleCenter;
                    break;
                case BtnStyle.Danger:
                    BaseColor = Color.FromArgb(201, 74, 66); HoverColor = Theme.Danger; PressColor = Color.FromArgb(166, 58, 51);
                    TextColor = Color.White;
                    TextAlign = ContentAlignment.MiddleCenter;
                    break;
                default:
                    BaseColor = Theme.Surface; HoverColor = Theme.Surface2; PressColor = Color.FromArgb(27, 28, 36);
                    TextColor = Theme.Fg;
                    TextAlign = ContentAlignment.MiddleLeft;
                    break;
            }
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _over = true; if (!IsDisposed) _timer.Start(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _over = false; _down = false; if (!IsDisposed) _timer.Start(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { _down = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _down = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color corner = Parent != null && Parent.BackColor.A == 255 ? Parent.BackColor : Theme.Bg;
            g.Clear(corner);

            Color fill = !Enabled ? Theme.Surface2 : _down ? PressColor : Theme.Lerp(BaseColor, HoverColor, _hover);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (_down) rect.Inflate(-1, -1);
            using (var path = Theme.RoundRect(rect, Radius))
            using (var br = new SolidBrush(fill))
                g.FillPath(br, path);

            bool left = TextAlign == ContentAlignment.MiddleLeft;
            var tr = new Rectangle(rect.X + (left ? 16 : 0), rect.Y + (_down ? 1 : 0), rect.Width - (left ? 16 : 0), rect.Height);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                        (left ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, Text, Font, tr, Enabled ? TextColor : Theme.Muted, flags);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Панель с самостоятельной фоновой анимацией: плавающие «боке»-круги, как расфокус в объективе.</summary>
    public class BokehPanel : Panel
    {
        class Dot { public float X, Y, R, VX, VY, Phase, Alpha; public Color C; }

        readonly List<Dot> _dots = new List<Dot>();
        readonly System.Windows.Forms.Timer _t = new System.Windows.Forms.Timer { Interval = 33 };
        readonly Random _rnd = new Random();

        public BokehPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            var palette = new[] { Theme.Accent, Theme.Teal, Color.FromArgb(255, 235, 210) };
            for (int i = 0; i < 22; i++)
            {
                _dots.Add(new Dot
                {
                    X = _rnd.Next(0, 1200),
                    Y = _rnd.Next(0, 800),
                    R = _rnd.Next(14, 70),
                    VX = (float)(_rnd.NextDouble() - 0.5) * 1.2f,
                    VY = (float)(_rnd.NextDouble() - 0.5) * 1.2f,
                    Phase = (float)(_rnd.NextDouble() * 6.28),
                    Alpha = _rnd.Next(25, 60),
                    C = palette[_rnd.Next(palette.Length)]
                });
            }
            _t.Tick += (s, e) => Step();
        }

        void Step()
        {
            if (IsDisposed || Disposing || !Visible) return;
            foreach (var d in _dots)
            {
                d.X += d.VX; d.Y += d.VY; d.Phase += 0.05f;
                if (Width > 0)
                {
                    if (d.X < -d.R) d.X = Width + d.R;
                    if (d.X > Width + d.R) d.X = -d.R;
                }
                if (Height > 0)
                {
                    if (d.Y < -d.R) d.Y = Height + d.R;
                    if (d.Y > Height + d.R) d.Y = -d.R;
                }
            }
            Invalidate();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            _t.Enabled = Visible && !DesignMode && !IsDisposed;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            var g = e.Graphics;
            using (var lg = new LinearGradientBrush(ClientRectangle, Color.FromArgb(18, 19, 26), Color.FromArgb(40, 31, 24), 60f))
                g.FillRectangle(lg, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var d in _dots)
            {
                float pulse = 0.75f + 0.25f * (float)Math.Sin(d.Phase);
                int a = Math.Clamp((int)(d.Alpha * pulse), 0, 255);
                using (var b = new SolidBrush(Color.FromArgb(a, d.C)))
                    g.FillEllipse(b, d.X - d.R, d.Y - d.R, d.R * 2, d.R * 2);
                using (var p = new Pen(Color.FromArgb(Math.Min(255, a + 30), d.C), 1.2f))
                    g.DrawEllipse(p, d.X - d.R, d.Y - d.R, d.R * 2, d.R * 2);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _t.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Карточка показателя: плавно появляется, число «набегает» от нуля.</summary>
    public class StatCard : Control
    {
        public string Caption = "";
        public double Target;
        public string Format = "N0";
        public string Suffix = "";
        float _p;
        int _animToken;
        System.Windows.Forms.Timer _delayTimer;

        public StatCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void Start(int delayMs)
        {
            if (IsDisposed || Disposing) return;
            int token = ++_animToken;
            _p = 0f;
            Invalidate();

            if (_delayTimer != null)
            {
                _delayTimer.Stop();
                _delayTimer.Dispose();
                _delayTimer = null;
            }

            _delayTimer = new System.Windows.Forms.Timer { Interval = Math.Max(1, delayMs) };
            _delayTimer.Tick += (s, e) =>
            {
                if (_delayTimer != null)
                {
                    _delayTimer.Stop();
                    _delayTimer.Dispose();
                    _delayTimer = null;
                }
                if (token != _animToken || IsDisposed || Disposing) return;
                Anim.Run(900, p =>
                {
                    if (token == _animToken && !IsDisposed)
                    {
                        _p = p;
                        Invalidate();
                    }
                }, null, true, this);
            };
            _delayTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 4 || Height < 4) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.Clear(Theme.Surface);

            using (var b = new SolidBrush(Theme.Accent))
                g.FillRectangle(b, 0, 0, 4, (int)(Height * _p));
            using (var pen = new Pen(Theme.Border))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            int a = Math.Clamp((int)(255 * _p), 0, 255);
            int dy = (int)((1f - _p) * 14f);

            var capRect = new RectangleF(14, 12 + dy, Math.Max(10, Width - 22), 20);
            using (var cb = new SolidBrush(Color.FromArgb(a, Theme.Muted)))
                g.DrawString(Caption, Theme.Small, cb, capRect);

            string txt = (Target * _p).ToString(Format, CultureInfo.CurrentCulture) + Suffix;
            var valRect = new RectangleF(12, 36 + dy, Math.Max(10, Width - 20), Math.Max(20, Height - 42));
            using (var vb = new SolidBrush(Color.FromArgb(a, Theme.Accent)))
                g.DrawString(txt, Theme.CardValue, vb, valRect);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _delayTimer != null)
            {
                _delayTimer.Stop();
                _delayTimer.Dispose();
                _delayTimer = null;
            }
            base.Dispose(disposing);
        }
    }
}