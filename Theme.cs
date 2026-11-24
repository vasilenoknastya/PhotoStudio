using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PhotoStudio
{
    /// <summary>Единый стиль: тёмная «проявочная» палитра + янтарный акцент, пара шрифтов Georgia (заголовки) / Segoe UI (текст).</summary>
    public static class Theme
    {
        public static readonly Color Bg = ColorTranslator.FromHtml("#16171D");
        public static readonly Color Surface = ColorTranslator.FromHtml("#22242E");
        public static readonly Color Surface2 = ColorTranslator.FromHtml("#2C2F3B");
        public static readonly Color Accent = ColorTranslator.FromHtml("#F2A541");
        public static readonly Color AccentHover = ColorTranslator.FromHtml("#FFBE5C");
        public static readonly Color AccentPress = ColorTranslator.FromHtml("#C98524");
        public static readonly Color Teal = ColorTranslator.FromHtml("#4FB0AE");
        public static readonly Color Fg = ColorTranslator.FromHtml("#ECEDF0");
        public static readonly Color Muted = ColorTranslator.FromHtml("#9AA0AE");
        public static readonly Color Danger = ColorTranslator.FromHtml("#E5584F");
        public static readonly Color Border = ColorTranslator.FromHtml("#3A3D4B");

        public static readonly Font Body = new Font("Segoe UI", 10f);
        public static readonly Font BodyBold = new Font("Segoe UI Semibold", 10f);
        public static readonly Font Small = new Font("Segoe UI", 8.5f);
        public static readonly Font H2 = new Font("Georgia", 14f, FontStyle.Bold);
        public static readonly Font Title = new Font("Georgia", 22f, FontStyle.Bold);
        public static readonly Font CardValue = new Font("Georgia", 22f, FontStyle.Bold);

        public static Color Lerp(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Label Lbl(string text, int x, int y, Font font = null, Color? color = null)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = font ?? Body,
                ForeColor = color ?? Fg,
                BackColor = Color.Transparent
            };
        }

        public static void StyleInput(Control c)
        {
            c.Font = Body;
            c.BackColor = Surface2;
            c.ForeColor = Fg;
            if (c is TextBox tb) tb.BorderStyle = BorderStyle.FixedSingle;
            else if (c is ComboBox cb) cb.FlatStyle = FlatStyle.Flat;
            else if (c is NumericUpDown nu) nu.BorderStyle = BorderStyle.FixedSingle;
        }

        public static void StyleGrid(DataGridView g)
        {
            g.BackgroundColor = Surface;
            g.BorderStyle = BorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.ColumnHeadersDefaultCellStyle.BackColor = Surface2;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Accent;
            g.ColumnHeadersDefaultCellStyle.Font = BodyBold;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Surface2;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 38;
            g.RowHeadersVisible = false;
            g.DefaultCellStyle.BackColor = Surface;
            g.DefaultCellStyle.ForeColor = Fg;
            g.DefaultCellStyle.Font = Body;
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(96, 72, 30);
            g.DefaultCellStyle.SelectionForeColor = Color.White;
            g.DefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(40, 43, 54);
            g.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(96, 72, 30);
            g.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
            g.GridColor = Border;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.RowTemplate.Height = 32;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.ReadOnly = true;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }
    }
}
