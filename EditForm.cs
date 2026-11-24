using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;

namespace PhotoStudio
{
    /// <summary>Универсальная форма добавления/редактирования: строится по списку полей Entity, проверяет ввод.</summary>
    public class EditForm : Form
    {
        readonly Entity _e;
        readonly long? _id;
        readonly Dictionary<string, Control> _ctl = new Dictionary<string, Control>();
        readonly ErrorProvider _err = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

        public EditForm(Entity e, long? id)
        {
            _e = e;
            _id = id;
            Text = (id == null ? "Добавление: " : "Редактирование: ") + e.Title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            ForeColor = Theme.Fg;
            Font = Theme.Body;

            int y = 18;
            Controls.Add(Theme.Lbl(id == null ? "Новая запись" : "Правка записи №" + id, 24, y, Theme.H2, Theme.Accent));
            y += 46;

            foreach (var f in e.Fields)
            {
                Controls.Add(Theme.Lbl(f.Label + (f.Required ? " *" : ""), 24, y, Theme.Small, Theme.Muted));
                y += 20;
                Control c = CreateControl(f);
                Theme.StyleInput(c);
                c.Location = new Point(24, y);
                c.Width = 400;
                if (f.Type == FT.Multiline) c.Height = 70;
                Controls.Add(c);
                _ctl[f.Col] = c;
                y += c.Height + 16;
            }

            var save = new AnimButton("Сохранить", 190) { Location = new Point(24, y + 6) };
            var cancel = new AnimButton("Отмена", 190, BtnStyle.Secondary) { Location = new Point(234, y + 6) };
            save.Click += (s, ev) => Commit();
            cancel.Click += (s, ev) => { DialogResult = DialogResult.Cancel; };
            Controls.Add(save);
            Controls.Add(cancel);
            AcceptButton = save;
            CancelButton = cancel;
            ClientSize = new Size(448, y + 62);

            if (id != null)
            {
                var dt = Db.Query("SELECT * FROM " + e.Table + " WHERE Id=@id", ("@id", id.Value));
                if (dt.Rows.Count > 0) Fill(dt.Rows[0]);
            }

            Shown += (s, ev) => Anim.FadeIn(this, 220);
        }

        Control CreateControl(Field f)
        {
            switch (f.Type)
            {
                case FT.Text:
                    return new TextBox { MaxLength = f.MaxLen };
                case FT.Multiline:
                    return new TextBox { MaxLength = f.MaxLen, Multiline = true, ScrollBars = ScrollBars.Vertical };
                case FT.Password:
                    return new TextBox { MaxLength = f.MaxLen, UseSystemPasswordChar = true, PlaceholderText = _id == null ? "минимум 6 символов" : "оставьте пустым, чтобы не менять" };
                case FT.Int:
                case FT.Decimal:
                    return new NumericUpDown
                    {
                        DecimalPlaces = f.Type == FT.Int ? 0 : f.Decimals,
                        Minimum = f.Min,
                        Maximum = f.Max,
                        Increment = f.Type == FT.Int ? 1 : (f.Decimals == 1 ? 0.5m : 1m),
                        TextAlign = HorizontalAlignment.Left
                    };
                case FT.Moment:
                    return new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy  HH:mm", Value = DateTime.Now };
                default:
                    var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                    if (f.Lookup != null)
                    {
                        var dt = Db.Query(f.Lookup);
                        cb.DataSource = dt;
                        cb.DisplayMember = dt.Columns[1].ColumnName;
                        cb.ValueMember = dt.Columns[0].ColumnName;
                        cb.SelectedIndex = -1;
                    }
                    else
                    {
                        cb.Items.AddRange(f.Options);
                        if (f.Default != null) cb.SelectedItem = f.Default;
                    }
                    return cb;
            }
        }

        void Fill(DataRow row)
        {
            foreach (var f in _e.Fields)
            {
                if (f.Type == FT.Password) continue;
                object v = row[f.Col];
                if (v == DBNull.Value) continue;
                Control c = _ctl[f.Col];
                switch (f.Type)
                {
                    case FT.Text:
                    case FT.Multiline:
                        c.Text = Convert.ToString(v);
                        break;
                    case FT.Int:
                    case FT.Decimal:
                        var n = (NumericUpDown)c;
                        n.Value = Math.Min(n.Maximum, Math.Max(n.Minimum, Convert.ToDecimal(v)));
                        break;
                    case FT.Moment:
                        DateTime d;
                        if (Db.TryParse(Convert.ToString(v), out d)) ((DateTimePicker)c).Value = d;
                        break;
                    case FT.Combo:
                        var cb = (ComboBox)c;
                        if (f.Lookup != null) cb.SelectedValue = Convert.ToInt64(v);
                        else cb.SelectedItem = Convert.ToString(v);
                        break;
                }
            }
        }

        void Commit()
        {
            ValidateChildren();
            _err.Clear();
            bool ok = true;
            var vals = new Dictionary<string, object>();

            foreach (var f in _e.Fields)
            {
                Control c = _ctl[f.Col];
                string msg = null;
                object val = null;

                switch (f.Type)
                {
                    case FT.Text:
                    case FT.Multiline:
                        string s = ((TextBox)c).Text.Trim();
                        if (s.Length == 0)
                        {
                            if (f.Required) msg = "Поле обязательно для заполнения";
                        }
                        else if (s.Length > f.MaxLen) msg = "Максимум " + f.MaxLen + " символов";
                        else if (f.Pattern != null && !Regex.IsMatch(s, f.Pattern)) msg = f.PatternMsg ?? "Неверный формат";
                        else val = s;
                        break;
                    case FT.Password:
                        string pw = ((TextBox)c).Text;
                        if (pw.Length == 0) { if (f.Required && _id == null) msg = "Введите пароль"; }
                        else if (pw.Length < 6) msg = "Пароль не короче 6 символов";
                        val = pw;
                        break;
                    case FT.Int:
                        val = (long)((NumericUpDown)c).Value;
                        break;
                    case FT.Decimal:
                        val = (double)((NumericUpDown)c).Value;
                        break;
                    case FT.Moment:
                        val = Db.Fmt(((DateTimePicker)c).Value);
                        break;
                    case FT.Combo:
                        var cb = (ComboBox)c;
                        if (f.Lookup != null)
                        {
                            if (cb.SelectedValue == null) msg = "Выберите значение из списка";
                            else val = Convert.ToInt64(cb.SelectedValue);
                        }
                        else
                        {
                            if (cb.SelectedItem == null) msg = "Выберите значение из списка";
                            else val = (string)cb.SelectedItem;
                        }
                        break;
                }

                if (msg != null) { _err.SetError(c, msg); ok = false; }
                vals[f.Col] = val;
            }

            if (!ok) return;

            if (_e.Validate != null)
            {
                string m = _e.Validate(vals, _id);
                if (m != null)
                {
                    MessageBox.Show(m, "Проверка данных", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            try
            {
                Repo.Save(_e, _id, vals);
                DialogResult = DialogResult.OK;
            }
            catch (SqliteException ex)
            {
                MessageBox.Show(Repo.Explain(ex), "Не удалось сохранить", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
