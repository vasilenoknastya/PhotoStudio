using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PhotoStudio
{
    /// <summary>Отчёты с выводом в CSV (открывается в Excel) и резервное копирование базы.</summary>
    public class ReportsPage : UserControl
    {
        class Rep { public string Name, Sql; public bool Dates; }

        readonly List<Rep> _reps = new List<Rep>();
        readonly ComboBox _cmb = new ComboBox();
        readonly DateTimePicker _from = new DateTimePicker();
        readonly DateTimePicker _to = new DateTimePicker();
        readonly DataGridView _grid = new DataGridView();
        readonly Label _sum = new Label();
        readonly Panel _bar;
        readonly Label _lblTitle, _lblRep, _lblFrom, _lblTo;
        readonly AnimButton _gen, _exp, _bkp;
        DataTable _data;

        public ReportsPage()
        {
            BackColor = Theme.Bg;

            _reps.Add(new Rep
            {
                Name = "Выручка по месяцам",
                Dates = true,
                Sql = """
                    SELECT substr(PaidAt,1,7) AS "Месяц", COUNT(*) AS "Платежей", ROUND(SUM(Amount),2) AS "Выручка, BYN"
                    FROM Payments
                    WHERE substr(PaidAt,1,10) BETWEEN @a AND @b
                    GROUP BY substr(PaidAt,1,7) ORDER BY 1
                    """
            });
            _reps.Add(new Rep
            {
                Name = "Задолженность по заказам",
                Dates = false,
                Sql = "SELECT \"Клиент\", \"Услуга\", \"Статус\", \"Сумма\", \"Оплачено\", \"Долг\" FROM (" +
                      Entities.Orders.SelectSql + ") WHERE \"Долг\" > 0.005 ORDER BY \"Долг\" DESC"
            });
            _reps.Add(new Rep
            {
                Name = "Загрузка фотографов",
                Dates = true,
                Sql = """
                    SELECT p.FullName AS "Фотограф", COUNT(sh.Id) AS "Съёмок",
                           ROUND(COALESCE(SUM(sh.Hours),0),1) AS "Часов",
                           ROUND(COALESCE(SUM(sv.PricePerHour*sh.Hours),0),2) AS "Сумма, BYN"
                    FROM Photographers p
                    LEFT JOIN Shoots sh ON sh.PhotographerId=p.Id AND sh.Status<>'Отменена'
                         AND substr(sh.ShootDate,1,10) BETWEEN @a AND @b
                    LEFT JOIN Services sv ON sv.Id=sh.ServiceId
                    GROUP BY p.Id ORDER BY 2 DESC
                    """
            });
            _reps.Add(new Rep
            {
                Name = "Популярные форматы печати",
                Dates = false,
                Sql = """
                    SELECT pf.Name AS "Формат печати", COUNT(o.Id) AS "Заказов",
                           COALESCE(SUM(o.Quantity),0) AS "Отпечатков",
                           ROUND(COALESCE(SUM(o.Quantity*pf.Price),0),2) AS "Сумма, BYN"
                    FROM PrintFormats pf
                    LEFT JOIN Orders o ON o.PrintFormatId=pf.Id
                    GROUP BY pf.Id ORDER BY 3 DESC
                    """
            });

            var status = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = Theme.Bg };
            _sum.AutoSize = true; _sum.ForeColor = Theme.Muted; _sum.Location = new Point(22, 8);
            status.Controls.Add(_sum);

            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(20, 4, 20, 0) };
            host.Controls.Add(_grid);
            Controls.Add(host);
            Controls.Add(status);

            _bar = new Panel { Dock = DockStyle.Top, Height = 156, BackColor = Theme.Bg };
            _lblTitle = Theme.Lbl("Отчёты", 20, 10, Theme.H2, Theme.Fg);
            _bar.Controls.Add(_lblTitle);

            _lblRep = Theme.Lbl("Отчёт", 20, 44, Theme.Small, Theme.Muted);
            _bar.Controls.Add(_lblRep);

            _cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            Theme.StyleInput(_cmb);
            foreach (var r in _reps) _cmb.Items.Add(r.Name);
            _cmb.SelectedIndex = 0;
            _bar.Controls.Add(_cmb);

            _lblFrom = Theme.Lbl("С даты", 20, 44, Theme.Small, Theme.Muted);
            _bar.Controls.Add(_lblFrom);
            _from.Format = DateTimePickerFormat.Short;
            _from.Value = DateTime.Today.AddMonths(-4);
            _bar.Controls.Add(_from);

            _lblTo = Theme.Lbl("По дату", 20, 44, Theme.Small, Theme.Muted);
            _bar.Controls.Add(_lblTo);
            _to.Format = DateTimePickerFormat.Short;
            _to.Value = DateTime.Today.AddMonths(1);
            _bar.Controls.Add(_to);

            _gen = new AnimButton("Сформировать", 140) { Height = 36 };
            _gen.Click += (s, ev) => Generate();
            _bar.Controls.Add(_gen);

            _exp = new AnimButton("Экспорт в CSV", 145, BtnStyle.Secondary) { Height = 36 };
            _exp.Click += (s, ev) => ExportCsv();
            _bar.Controls.Add(_exp);

            if (Session.IsAdmin)
            {
                _bkp = new AnimButton("Резервная копия БД", 190, BtnStyle.Secondary) { Height = 32 };
                _bkp.Click += (s, ev) => BackupDb();
                _bar.Controls.Add(_bkp);
            }
            Controls.Add(_bar);

            _cmb.SelectedIndexChanged += (s, ev) =>
            {
                bool d = _reps[_cmb.SelectedIndex].Dates;
                _from.Enabled = d; _to.Enabled = d;
            };

            _bar.Resize += (s, ev) => Relayout();
            Relayout();
            Generate();
        }

        void Relayout()
        {
            int m = 20;
            int w = Math.Max(320, _bar.Width - 2 * m);

            if (w >= 900)
            {
                _bar.Height = 148;
                int cmbW = Math.Clamp(w - 610, 200, 300);
                _lblRep.Location = new Point(m, 44);
                _cmb.SetBounds(m, 62, cmbW, 28);

                int fx = _cmb.Right + 16;
                _lblFrom.Location = new Point(fx, 44);
                _from.SetBounds(fx, 62, 124, 28);

                int tx = _from.Right + 14;
                _lblTo.Location = new Point(tx, 44);
                _to.SetBounds(tx, 62, 124, 28);

                int gx = _to.Right + 16;
                int genW = 136;
                int expW = 140;
                if (gx + genW + 10 + expW > m + w)
                {
                    genW = Math.Max(110, (m + w - gx - 10) / 2);
                    expW = genW;
                }
                _gen.SetBounds(gx, 58, genW, 36);
                _exp.SetBounds(gx + genW + 10, 58, expW, 36);

                if (_bkp != null) _bkp.Location = new Point(m, 102);
            }
            else
            {
                _bar.Height = _bkp != null ? 184 : 154;
                int cmbW = Math.Clamp(w - 280, 180, 320);
                _lblRep.Location = new Point(m, 44);
                _cmb.SetBounds(m, 62, cmbW, 28);

                int fx = _cmb.Right + 14;
                _lblFrom.Location = new Point(fx, 44);
                _from.SetBounds(fx, 62, 118, 28);

                int tx = _from.Right + 12;
                _lblTo.Location = new Point(tx, 44);
                _to.SetBounds(tx, 62, 118, 28);

                int row2Y = 102;
                _gen.SetBounds(m, row2Y, 136, 36);
                _exp.SetBounds(m + 146, row2Y, 145, 36);
                if (_bkp != null) _bkp.Location = new Point(m + 301, row2Y + 2);
            }
        }

        void Generate()
        {
            var r = _reps[_cmb.SelectedIndex];
            DataTable dt;
            if (r.Dates)
            {
                if (_from.Value.Date > _to.Value.Date)
                {
                    MessageBox.Show("Дата «С» не может быть позже даты «По».", "Проверка данных", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                dt = Db.Query(r.Sql, ("@a", Db.FmtDate(_from.Value)), ("@b", Db.FmtDate(_to.Value)));
            }
            else dt = Db.Query(r.Sql);

            _data = dt;
            _grid.DataSource = dt;
            foreach (DataGridViewColumn col in _grid.Columns)
                if (col.ValueType == typeof(double))
                {
                    col.DefaultCellStyle.Format = "N2";
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }

            string text = "Строк: " + dt.Rows.Count;
            var last = dt.Columns.Cast<DataColumn>().LastOrDefault(c => c.DataType == typeof(double));
            if (last != null)
            {
                double total = 0;
                foreach (DataRow row in dt.Rows) if (row[last] != DBNull.Value) total += (double)row[last];
                text += "     Итого по «" + last.ColumnName + "»: " + total.ToString("N2");
            }
            _sum.Text = text;
        }

        static string Q(string s)
        {
            if (s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        void ExportCsv()
        {
            if (_data == null || _data.Rows.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта. Нажмите «Сформировать».", "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string name = _reps[_cmb.SelectedIndex].Name;
            using (var sfd = new SaveFileDialog
            {
                Filter = "CSV для Excel (*.csv)|*.csv",
                FileName = name + "_" + DateTime.Now.ToString("yyyyMMdd") + ".csv"
            })
            {
                if (sfd.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(string.Join(";", _data.Columns.Cast<DataColumn>().Select(c => Q(c.ColumnName))));
                    foreach (DataRow r in _data.Rows)
                        sb.AppendLine(string.Join(";", r.ItemArray.Select(v =>
                            Q(v is DBNull ? "" : Convert.ToString(v, CultureInfo.CurrentCulture)))));
                    File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
                    Db.Log("Экспорт отчёта «" + name + "» в CSV");
                    MessageBox.Show("Отчёт сохранён:\n" + sfd.FileName, "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось сохранить файл:\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        void BackupDb()
        {
            using (var sfd = new SaveFileDialog
            {
                Filter = "База данных SQLite (*.db)|*.db",
                FileName = "studio_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".db"
            })
            {
                if (sfd.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    Db.Backup(sfd.FileName);
                    Db.Log("Резервная копия БД: " + Path.GetFileName(sfd.FileName));
                    MessageBox.Show("Резервная копия создана:\n" + sfd.FileName, "Резервное копирование", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось создать копию:\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}