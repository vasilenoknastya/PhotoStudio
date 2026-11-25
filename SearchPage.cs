using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PhotoStudio
{
    /// <summary>Сквозной поиск по клиентам, съёмкам, заказам, оплатам и фотографам. Двойной щелчок — переход в раздел.</summary>
    public class SearchPage : UserControl
    {
        const string Sql = """
            SELECT 'Клиент' AS "Раздел", FullName AS "Название", Phone||COALESCE(' • '||Email,'') AS "Детали", 'clients' AS "Key",
                   FullName||' '||Phone||' '||COALESCE(Email,'')||' '||COALESCE(Notes,'') AS "Txt"
            FROM Clients
            UNION ALL
            SELECT 'Съёмка', c.FullName||' — '||sv.Name, sh.ShootDate||' • '||sh.Status||' • '||COALESCE(sh.Location,''), 'shoots',
                   c.FullName||' '||sv.Name||' '||sh.ShootDate||' '||sh.Status||' '||COALESCE(sh.Location,'')||' '||p.FullName
            FROM Shoots sh JOIN Clients c ON c.Id=sh.ClientId JOIN Services sv ON sv.Id=sh.ServiceId JOIN Photographers p ON p.Id=sh.PhotographerId
            UNION ALL
            SELECT 'Заказ', '№'||o.Id||' '||c.FullName, o.Status||' • '||pf.Name||' × '||o.Quantity, 'orders',
                   '№'||o.Id||' '||c.FullName||' '||o.Status||' '||pf.Name
            FROM Orders o JOIN Shoots sh ON sh.Id=o.ShootId JOIN Clients c ON c.Id=sh.ClientId JOIN PrintFormats pf ON pf.Id=o.PrintFormatId
            UNION ALL
            SELECT 'Оплата', '№'||p.OrderId||' '||c.FullName, ROUND(p.Amount,2)||' BYN • '||p.Method||' • '||p.PaidAt, 'payments',
                   '№'||p.OrderId||' '||c.FullName||' '||p.Method||' '||p.PaidAt
            FROM Payments p JOIN Orders o ON o.Id=p.OrderId JOIN Shoots sh ON sh.Id=o.ShootId JOIN Clients c ON c.Id=sh.ClientId
            UNION ALL
            SELECT 'Фотограф', FullName, Specialty||' • '||Phone, 'photographers', FullName||' '||Specialty||' '||Phone
            FROM Photographers
            """;

        readonly DataGridView _grid = new DataGridView();
        readonly ComboBox _section = new ComboBox();
        readonly Label _count = new Label();
        readonly DataView _view;
        readonly string _q;
        readonly Action<string> _go;

        public SearchPage(string q, Action<string> go)
        {
            _q = q;
            _go = go;
            BackColor = Theme.Bg;

            var table = Db.Query(Sql);
            _view = new DataView(table) { Sort = "[Раздел], [Название]" };

            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            // Колонки у DataGridView появляются только после привязки к контексту формы,
            // поэтому скрываем служебные столбцы в событии завершения привязки, а не сразу.
            _grid.DataBindingComplete += (s, ev) =>
            {
                if (_grid.Columns.Contains("Key")) _grid.Columns["Key"].Visible = false;
                if (_grid.Columns.Contains("Txt")) _grid.Columns["Txt"].Visible = false;
            };
            _grid.DataSource = _view;
            _grid.CellDoubleClick += (s, ev) => { if (ev.RowIndex >= 0) OpenRow(); };

            var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(20, 4, 20, 0) };
            host.Controls.Add(_grid);
            Controls.Add(host);

            var status = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Theme.Bg };
            _count.AutoSize = true; _count.ForeColor = Theme.Muted; _count.Font = Theme.Small; _count.Location = new Point(22, 7);
            status.Controls.Add(_count);
            Controls.Add(status);

            var bar = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Theme.Bg };
            bar.Controls.Add(Theme.Lbl("Результаты поиска: «" + q + "»", 20, 12, Theme.H2, Theme.Fg));
            bar.Controls.Add(Theme.Lbl("Раздел:", 20, 58, Theme.Body, Theme.Muted));
            _section.DropDownStyle = ComboBoxStyle.DropDownList;
            _section.SetBounds(86, 54, 180, 28);
            Theme.StyleInput(_section);
            _section.Items.Add("Все разделы");
            foreach (var v in table.AsEnumerable().Select(r => (string)r["Раздел"]).Distinct().OrderBy(x => x))
                _section.Items.Add(v);
            _section.SelectedIndex = 0;
            _section.SelectedIndexChanged += (s, ev) => Apply();
            bar.Controls.Add(_section);
            bar.Controls.Add(Theme.Lbl("Двойной щелчок по строке — открыть раздел", 290, 58, Theme.Small, Theme.Muted));
            Controls.Add(bar);

            Apply();
        }

        void Apply()
        {
            string f = "[Txt] LIKE '%" + Db.EscLike(_q) + "%'";
            if (_section.SelectedIndex > 0) f += " AND [Раздел] = '" + Db.EscStr(_section.Text) + "'";
            _view.RowFilter = f;
            _count.Text = "Найдено: " + _view.Count;
        }

        void OpenRow()
        {
            var drv = _grid.CurrentRow == null ? null : _grid.CurrentRow.DataBoundItem as DataRowView;
            if (drv == null) return;
            string key = Convert.ToString(drv["Key"]);
            var ent = Entities.Get(key);
            if (ent == null) return;
            if (ent.AdminOnly && !Session.IsAdmin)
            {
                MessageBox.Show("Раздел доступен только администратору.", "Доступ запрещён", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _go(key);
        }
    }
}
