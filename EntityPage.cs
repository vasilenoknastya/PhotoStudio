using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PhotoStudio
{
    /// <summary>Универсальная страница таблицы: просмотр, поиск, фильтр, сортировка по заголовку, добавление/правка/удаление.</summary>
    public class EntityPage : UserControl
    {
        readonly Entity _e;
        readonly DataGridView _grid = new DataGridView();
        readonly ComboBox _filter = new ComboBox();
        readonly TextBox _search = new TextBox();
        readonly Label _count = new Label();
        DataTable _table;
        DataView _view;
        bool _suppress;

        public EntityPage(Entity e)
        {
            _e = e;
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            BuildUi();
            LoadData();
        }

        void BuildUi()
        {
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.CellDoubleClick += (s, ev) => { if (ev.RowIndex >= 0) EditSelected(); };
            _grid.CellFormatting += (s, ev) =>
            {
                if (ev.RowIndex < 0) return;
                string name = _grid.Columns[ev.ColumnIndex].Name;
                if (name == "Долг" && ev.Value is double d && d > 0.005) ev.CellStyle.ForeColor = Theme.Danger;
                else if (name == "Статус" && ev.Value is string st)
                    ev.CellStyle.ForeColor = st == "Отменена" ? Theme.Muted : (st == "Выдан" || st == "Проведена") ? Theme.Teal : Theme.Fg;
            };

            var gridHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(20, 4, 20, 0) };
            gridHost.Controls.Add(_grid);
            Controls.Add(gridHost);

            var status = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Theme.Bg };
            _count.AutoSize = true;
            _count.ForeColor = Theme.Muted;
            _count.Font = Theme.Small;
            _count.Location = new Point(22, 7);
            status.Controls.Add(_count);
            Controls.Add(status);

            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 50),
                BackColor = Theme.Bg,
                Padding = new Padding(20, 6, 20, 6),
                WrapContents = true
            };

            if (_e.FilterColumn != null)
            {
                bar.Controls.Add(new Label
                {
                    Text = _e.FilterColumn + ":",
                    AutoSize = true,
                    ForeColor = Theme.Muted,
                    Margin = new Padding(0, 9, 6, 0),
                    BackColor = Theme.Bg
                });
                _filter.DropDownStyle = ComboBoxStyle.DropDownList;
                _filter.Width = 150;
                _filter.Margin = new Padding(0, 5, 16, 4);
                Theme.StyleInput(_filter);
                _filter.SelectedIndexChanged += (s, ev) => { if (!_suppress) ApplyFilter(); };
                bar.Controls.Add(_filter);
            }

            _search.Width = 220;
            _search.Margin = new Padding(0, 5, 16, 4);
            _search.PlaceholderText = "Поиск по таблице…";
            Theme.StyleInput(_search);
            _search.TextChanged += (s, ev) => ApplyFilter();
            bar.Controls.Add(_search);

            if (!_e.ReadOnly)
            {
                var add = new AnimButton("Добавить", 100) { Margin = new Padding(0, 2, 8, 4) };
                var edit = new AnimButton("Изменить", 100, BtnStyle.Secondary) { Margin = new Padding(0, 2, 8, 4) };
                add.Click += (s, ev) => AddNew();
                edit.Click += (s, ev) => EditSelected();
                bar.Controls.Add(add);
                bar.Controls.Add(edit);
                if (Session.IsAdmin)
                {
                    var del = new AnimButton("Удалить", 100, BtnStyle.Danger) { Margin = new Padding(0, 2, 8, 4) };
                    del.Click += (s, ev) => DeleteSelected();
                    bar.Controls.Add(del);
                }
            }
            var refresh = new AnimButton("Обновить", 100, BtnStyle.Secondary) { Margin = new Padding(0, 2, 0, 4) };
            refresh.Click += (s, ev) => LoadData();
            bar.Controls.Add(refresh);
            Controls.Add(bar);

            var head = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Theme.Bg };
            head.Controls.Add(Theme.Lbl(_e.Title, 20, 12, Theme.H2, Theme.Fg));
            Controls.Add(head);
        }

        public void LoadData()
        {
            string prev = _filter.SelectedItem as string;
            _table = Db.Query(_e.SelectSql);
            _view = new DataView(_table);
            if (!string.IsNullOrEmpty(_e.Sort)) _view.Sort = _e.Sort;
            _grid.DataSource = _view;

            if (_grid.Columns.Contains("Id")) _grid.Columns["Id"].Visible = false;
            foreach (DataGridViewColumn col in _grid.Columns)
            {
                col.MinimumWidth = 60;
                if (col.ValueType == typeof(double))
                {
                    col.DefaultCellStyle.Format = "N2";
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
            }

            if (_e.FilterColumn != null && _table.Columns.Contains(_e.FilterColumn))
            {
                _suppress = true;
                _filter.Items.Clear();
                _filter.Items.Add("Все");
                var values = _table.AsEnumerable()
                    .Select(r => Convert.ToString(r[_e.FilterColumn]))
                    .Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x);
                foreach (var v in values) _filter.Items.Add(v);
                _filter.SelectedItem = prev != null && _filter.Items.Contains(prev) ? prev : "Все";
                _suppress = false;
            }
            ApplyFilter();
        }

        void ApplyFilter()
        {
            if (_view == null) return;
            var parts = new List<string>();
            if (_e.FilterColumn != null && _filter.SelectedIndex > 0)
                parts.Add("[" + _e.FilterColumn + "] = '" + Db.EscStr(_filter.Text) + "'");

            string q = _search.Text.Trim();
            if (q.Length > 0)
            {
                string like = Db.EscLike(q);
                var cols = _table.Columns.Cast<DataColumn>()
                    .Where(c => c.ColumnName != "Id")
                    .Select(c => "Convert([" + c.ColumnName + "], 'System.String') LIKE '%" + like + "%'");
                parts.Add("(" + string.Join(" OR ", cols) + ")");
            }

            try { _view.RowFilter = string.Join(" AND ", parts); }
            catch (Exception) { _view.RowFilter = ""; }
            _count.Text = "Записей: " + _view.Count + " из " + _table.Rows.Count;
        }

        long? SelectedId()
        {
            if (_grid.CurrentRow == null) return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            if (drv == null) return null;
            return Convert.ToInt64(drv["Id"]);
        }

        void AddNew()
        {
            using (var f = new EditForm(_e, null))
                if (f.ShowDialog(FindForm()) == DialogResult.OK) LoadData();
        }

        void EditSelected()
        {
            if (_e.ReadOnly) return;
            long? id = SelectedId();
            if (id == null)
            {
                MessageBox.Show("Выберите запись в таблице.", "Изменение", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var f = new EditForm(_e, id))
                if (f.ShowDialog(FindForm()) == DialogResult.OK) LoadData();
        }

        void DeleteSelected()
        {
            long? id = SelectedId();
            if (id == null)
            {
                MessageBox.Show("Выберите запись в таблице.", "Удаление", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_e.Table == "Users" && id.Value == Session.UserId)
            {
                MessageBox.Show("Нельзя удалить собственную учётную запись.", "Удаление", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var ans = MessageBox.Show("Удалить выбранную запись из раздела «" + _e.Title + "»?\nДействие нельзя отменить.",
                "Подтверждение удаления", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (ans != DialogResult.Yes) return;

            string err;
            if (Repo.Delete(_e, id.Value, out err)) LoadData();
            else MessageBox.Show(err, "Не удалось удалить", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}