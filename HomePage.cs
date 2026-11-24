using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace PhotoStudio
{
    /// <summary>Главный экран: анимированный фон, карточки показателей, быстрые действия, ближайшие съёмки.</summary>
    public class HomePage : BokehPanel
    {
        readonly Action<string> _go;
        readonly Label _hello, _sub, _gridTitle;
        readonly List<StatCard> _cards = new List<StatCard>();
        readonly List<AnimButton> _quickBtns = new List<AnimButton>();
        readonly Panel _quick, _gridHost;
        readonly DataGridView _grid = new DataGridView();

        public HomePage(Action<string> go)
        {
            _go = go;
            int h = DateTime.Now.Hour;
            string part = h < 12 ? "Доброе утро" : h < 18 ? "Добрый день" : "Добрый вечер";
            _hello = new Label
            {
                Text = part + ", " + Session.FullName + "!",
                Font = Theme.Title,
                ForeColor = Theme.Fg,
                BackColor = Color.Transparent,
                AutoSize = true
            };
            _sub = new Label
            {
                Text = "Сегодня " + DateTime.Now.ToString("dddd, d MMMM yyyy") + " — вот что происходит в студии",
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                AutoSize = true
            };
            Controls.Add(_hello);
            Controls.Add(_sub);

            string[] caps = { "КЛИЕНТОВ В БАЗЕ", "СЪЁМОК НА 7 ДНЕЙ", "ЗАКАЗОВ В ПЕЧАТИ", "ВЫРУЧКА ЗА МЕСЯЦ", "ДОЛГ КЛИЕНТОВ" };
            foreach (string c in caps)
            {
                var card = new StatCard { Caption = c };
                _cards.Add(card);
                Controls.Add(card);
            }
            _cards[3].Suffix = " BYN";
            _cards[4].Suffix = " BYN";

            _quick = new Panel { BackColor = Color.Transparent };
            Action<string, int, BtnStyle, Action> addBtn = (text, w, style, act) =>
            {
                var b = new AnimButton(text, w, style) { Height = 36 };
                b.Click += (s, ev) => act();
                _quick.Controls.Add(b);
                _quickBtns.Add(b);
            };
            addBtn("+ Клиент", 120, BtnStyle.Primary, () => QuickAdd(Entities.Clients));
            addBtn("+ Съёмка", 120, BtnStyle.Primary, () => QuickAdd(Entities.Shoots));
            addBtn("+ Заказ", 120, BtnStyle.Primary, () => QuickAdd(Entities.Orders));
            addBtn("+ Оплата", 120, BtnStyle.Primary, () => QuickAdd(Entities.Payments));
            addBtn("Калькулятор", 130, BtnStyle.Secondary, () => _go("calc"));
            addBtn("Отчёты", 110, BtnStyle.Secondary, () => _go("reports"));
            Controls.Add(_quick);

            _gridTitle = new Label
            {
                Text = "Ближайшие съёмки",
                Font = Theme.H2,
                ForeColor = Theme.Accent,
                BackColor = Color.Transparent,
                AutoSize = true
            };
            Controls.Add(_gridTitle);

            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.CellDoubleClick += (s, ev) => { if (ev.RowIndex >= 0) _go("shoots"); };
            _gridHost = new Panel { BackColor = Theme.Surface };
            _gridHost.Controls.Add(_grid);
            Controls.Add(_gridHost);

            Resize += (s, ev) => Relayout();
            Reload(animate: true);
            Relayout();
        }

        void Relayout()
        {
            int m = 28;
            int w = Math.Max(320, Width - 2 * m);
            _hello.Location = new Point(m, 16);
            _sub.Location = new Point(m + 2, 62);

            int gap = 12;
            int cw = Math.Max(110, (w - gap * (_cards.Count - 1)) / _cards.Count);
            for (int i = 0; i < _cards.Count; i++)
                _cards[i].SetBounds(m + i * (cw + gap), 98, cw, 96);

            int qx = 0, qy = 2, rowH = 40;
            foreach (var b in _quickBtns)
            {
                if (qx > 0 && qx + b.Width > w)
                {
                    qx = 0;
                    qy += rowH;
                }
                b.Location = new Point(qx, qy);
                qx += b.Width + 10;
            }
            int quickH = qy + rowH;
            _quick.SetBounds(m, 206, w, quickH);

            int titleY = _quick.Bottom + 12;
            _gridTitle.Location = new Point(m, titleY);

            int hostY = titleY + 32;
            _gridHost.SetBounds(m, hostY, w, Math.Max(100, Height - hostY - 20));
        }

        void QuickAdd(Entity e)
        {
            using (var f = new EditForm(e, null))
                if (f.ShowDialog(FindForm()) == DialogResult.OK) Reload(animate: true);
        }

        static double Sc(string sql, params (string, object)[] ps)
        {
            object o = Db.Scalar(sql, ps);
            return o == null ? 0 : Convert.ToDouble(o);
        }

        void Reload(bool animate = false)
        {
            DateTime now = DateTime.Now;
            _cards[0].Target = Sc("SELECT COUNT(*) FROM Clients");
            _cards[1].Target = Sc("SELECT COUNT(*) FROM Shoots WHERE Status='Запланирована' AND ShootDate>=@a AND ShootDate<@b",
                ("@a", Db.Fmt(now)), ("@b", Db.Fmt(now.AddDays(7))));
            _cards[2].Target = Sc("SELECT COUNT(*) FROM Orders WHERE Status='В печати'");
            _cards[3].Target = Sc("SELECT COALESCE(SUM(Amount),0) FROM Payments WHERE PaidAt>=@m",
                ("@m", Db.FmtDate(new DateTime(now.Year, now.Month, 1))));
            _cards[4].Target = Sc("SELECT COALESCE(SUM(\"Долг\"),0) FROM (" + Entities.Orders.SelectSql + ")");

            if (animate)
            {
                for (int i = 0; i < _cards.Count; i++)
                    _cards[i].Start(i * 120);
            }
            else
            {
                foreach (var c in _cards) c.Invalidate();
            }

            var dt = Db.Query("""
                SELECT c.FullName AS "Клиент", sv.Name AS "Услуга", sh.ShootDate AS "Дата и время",
                       p.FullName AS "Фотограф", sh.Location AS "Место"
                FROM Shoots sh
                JOIN Clients c ON c.Id=sh.ClientId
                JOIN Services sv ON sv.Id=sh.ServiceId
                JOIN Photographers p ON p.Id=sh.PhotographerId
                WHERE sh.Status='Запланирована' AND sh.ShootDate>=@now
                ORDER BY sh.ShootDate LIMIT 10
                """, ("@now", Db.Fmt(now)));
            _grid.DataSource = dt;
        }
    }
}