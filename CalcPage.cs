using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PhotoStudio
{
    /// <summary>Калькулятор стоимости: съёмка + печать, срочность, скидка. Считает на лету.</summary>
    public class CalcPage : UserControl
    {
        readonly ComboBox _svc = new ComboBox();
        readonly ComboBox _fmt = new ComboBox();
        readonly NumericUpDown _hours = new NumericUpDown();
        readonly NumericUpDown _qty = new NumericUpDown();
        readonly NumericUpDown _disc = new NumericUpDown();
        readonly CheckBox _urgent = new CheckBox();
        readonly Label _title, _lShoot, _lPrint, _lDisc, _lTotal;
        readonly Panel _card, _res;
        readonly AnimButton _copy, _reset, _book;

        public CalcPage()
        {
            BackColor = Theme.Bg;
            DoubleBuffered = true;

            _title = Theme.Lbl("Калькулятор стоимости", 24, 14, Theme.H2, Theme.Fg);
            Controls.Add(_title);

            _card = new Panel { Size = new Size(430, 460), BackColor = Theme.Surface };
            Controls.Add(_card);

            int y = 20;
            Action<string, Control> row = (label, ctl) =>
            {
                _card.Controls.Add(Theme.Lbl(label, 24, y, Theme.Small, Theme.Muted));
                y += 20;
                ctl.Location = new Point(24, y);
                ctl.Width = 382;
                Theme.StyleInput(ctl);
                _card.Controls.Add(ctl);
                y += ctl.Height + 14;
            };

            var sv = Db.Query("SELECT Id, Name, PricePerHour FROM Services ORDER BY Name");
            _svc.DropDownStyle = ComboBoxStyle.DropDownList;
            _svc.DataSource = sv;
            _svc.DisplayMember = "Name";
            _svc.ValueMember = "Id";
            row("Услуга", _svc);

            _hours.DecimalPlaces = 1;
            _hours.Minimum = 0.5m;
            _hours.Maximum = 12m;
            _hours.Increment = 0.5m;
            _hours.Value = 1m;
            row("Длительность съёмки, ч", _hours);

            var pf = Db.Query("SELECT Id, Name, Price FROM PrintFormats ORDER BY Price");
            _fmt.DropDownStyle = ComboBoxStyle.DropDownList;
            _fmt.DataSource = pf;
            _fmt.DisplayMember = "Name";
            _fmt.ValueMember = "Id";
            row("Формат печати", _fmt);

            _qty.Minimum = 0;
            _qty.Maximum = 1000;
            _qty.Value = 10;
            row("Количество отпечатков", _qty);

            _disc.Minimum = 0;
            _disc.Maximum = 50;
            _disc.Increment = 5;
            _disc.Value = 0;
            row("Скидка, %", _disc);

            _urgent.Text = "Срочная печать (+20% к стоимости печати)";
            _urgent.AutoSize = false;
            _urgent.Size = new Size(382, 24);
            _urgent.ForeColor = Theme.Fg;
            _urgent.BackColor = Theme.Surface;
            _urgent.Location = new Point(24, y);
            _card.Controls.Add(_urgent);

            // результат
            _res = new Panel { Size = new Size(400, 304), BackColor = Theme.Surface };
            Controls.Add(_res);
            _res.Controls.Add(Theme.Lbl("Расчёт", 24, 18, Theme.H2, Theme.Accent));
            _lShoot = Theme.Lbl("", 24, 66, Theme.Body); _res.Controls.Add(_lShoot);
            _lPrint = Theme.Lbl("", 24, 100, Theme.Body); _res.Controls.Add(_lPrint);
            _lDisc = Theme.Lbl("", 24, 134, Theme.Body); _res.Controls.Add(_lDisc);
            _res.Controls.Add(Theme.Lbl("ИТОГО", 24, 184, Theme.Small, Theme.Muted));
            _lTotal = Theme.Lbl("", 24, 202, Theme.Title, Theme.Accent); _res.Controls.Add(_lTotal);

            _copy = new AnimButton("Копировать итог", 180, BtnStyle.Secondary) { Location = new Point(24, 254), Height = 36 };
            _copy.Click += (s, ev) =>
            {
                if (!string.IsNullOrWhiteSpace(_lTotal.Text))
                    Clipboard.SetText(_lTotal.Text);
            };

            _reset = new AnimButton("Сбросить", 110, BtnStyle.Secondary) { Location = new Point(214, 254), Height = 36 };
            _reset.Click += (s, ev) =>
            {
                _hours.Value = 1m;
                _qty.Value = 10;
                _disc.Value = 0;
                _urgent.Checked = false;
                if (_svc.Items.Count > 0) _svc.SelectedIndex = 0;
                if (_fmt.Items.Count > 0) _fmt.SelectedIndex = 0;
                Recalc();
            };

            _res.Controls.Add(_copy);
            _res.Controls.Add(_reset);

            _book = new AnimButton("Оформить съёмку по расчёту…", 400) { Height = 38 };
            _book.Click += (s, ev) =>
            {
                using (var f = new EditForm(Entities.Shoots, null))
                    f.ShowDialog(FindForm());
            };
            Controls.Add(_book);

            _svc.SelectedIndexChanged += (s, ev) => Recalc();
            _fmt.SelectedIndexChanged += (s, ev) => Recalc();
            _hours.ValueChanged += (s, ev) => Recalc();
            _qty.ValueChanged += (s, ev) => Recalc();
            _disc.ValueChanged += (s, ev) => Recalc();
            _urgent.CheckedChanged += (s, ev) => Recalc();

            Resize += (s, ev) => Relayout();
            Recalc();
            Relayout();
        }

        void Relayout()
        {
            int m = 24;
            int availW = Math.Max(320, Width - 2 * m);
            _title.Location = new Point(m, 14);

            bool sideBySide = availW >= 854;
            int leftW = sideBySide ? 430 : Math.Min(480, availW);
            int rightW = sideBySide ? Math.Min(420, availW - leftW - 24) : Math.Min(480, availW);

            _card.SetBounds(m, 58, leftW, 460);
            int innerW = Math.Max(220, leftW - 48);
            foreach (Control ctl in _card.Controls)
            {
                if (ctl is ComboBox || ctl is NumericUpDown || ctl is CheckBox)
                    ctl.Width = innerW;
            }

            int rx = sideBySide ? m + leftW + 24 : m;
            int ry = sideBySide ? 58 : _card.Bottom + 20;
            _res.SetBounds(rx, ry, rightW, 304);

            int btnGap = 10;
            int resetW = Math.Min(110, Math.Max(80, (rightW - 48 - btnGap) / 3));
            int copyW = Math.Max(110, rightW - 48 - resetW - btnGap);
            _copy.SetBounds(24, 254, copyW, 36);
            _reset.SetBounds(24 + copyW + btnGap, 254, resetW, 36);

            _book.SetBounds(rx, _res.Bottom + 16, rightW, 38);
        }

        void Recalc()
        {
            if (_lTotal == null) return;
            var sv = _svc.SelectedItem as DataRowView;
            var pf = _fmt.SelectedItem as DataRowView;
            if (sv == null || pf == null) return;

            double shoot = Convert.ToDouble(sv["PricePerHour"]) * (double)_hours.Value;
            double print = Convert.ToDouble(pf["Price"]) * (double)_qty.Value * (_urgent.Checked ? 1.2 : 1.0);
            double sub = shoot + print;
            double disc = sub * (double)_disc.Value / 100.0;

            _lShoot.Text = "Съёмка:  " + shoot.ToString("N2") + " BYN";
            _lPrint.Text = "Печать:  " + print.ToString("N2") + " BYN";
            _lDisc.Text = "Скидка:  −" + disc.ToString("N2") + " BYN";
            _lTotal.Text = (sub - disc).ToString("N2") + " BYN";
        }
    }
}