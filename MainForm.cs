using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace PhotoStudio
{
    /// <summary>Главное окно: шапка (header) с глобальным поиском, хлебные крошки, меню по ролям, подвал (footer), плавные переходы между экранами.</summary>
    public class MainForm : Form
    {
        public bool Logout;

        Panel _header, _crumbBar, _side, _footer, _content;
        FlowLayoutPanel _crumbs;
        TextBox _search;
        AnimButton _btnSearch, _btnOut, _aboutBtn;
        Label _userLabel, _clock;
        readonly Dictionary<string, AnimButton> _nav = new Dictionary<string, AnimButton>();
        Control _current;
        int _navId;
        System.Windows.Forms.Timer _tick;

        public MainForm()
        {
            Text = AppInfo.Name + " — " + Session.FullName;
            MinimumSize = new Size(1060, 720);
            Size = new Size(1280, 780);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Fg;
            Font = Theme.Body;
            AppIcon.Apply(this);

            // порядок добавления важен для стыковки (Dock): сначала Fill, последним — самая верхняя панель
            _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            EnableDoubleBuffer(_content);
            Controls.Add(_content);

            BuildSidebar();
            BuildFooter();
            BuildCrumbs();
            BuildHeader();

            Load += (s, ev) => { Anim.FadeIn(this); Go("home"); };
            FormClosed += (s, ev) => { if (_tick != null) _tick.Dispose(); };
        }

        static void EnableDoubleBuffer(Control control)
        {
            if (control == null) return;
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, control, new object[] { true });
        }

        // ---------------- шапка ----------------
        void BuildHeader()
        {
            _header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Theme.Surface };
            EnableDoubleBuffer(_header);

            var logo = Theme.Lbl("◉ ОБЪЕКТИВ", 20, 8, new Font("Georgia", 18f, FontStyle.Bold), Theme.Accent);
            var sub = Theme.Lbl("фотостудия", 24, 40, Theme.Small, Theme.Muted);
            _header.Controls.Add(logo);
            _header.Controls.Add(sub);

            _search = new TextBox { PlaceholderText = "Глобальный поиск (Ctrl+F)…" };
            Theme.StyleInput(_search);
            _search.KeyDown += (s, ev) =>
            {
                if (ev.KeyCode == Keys.Enter) { ev.SuppressKeyPress = true; DoSearch(); }
            };
            _header.Controls.Add(_search);

            _btnSearch = new AnimButton("Найти", 90) { Height = 38 };
            _btnSearch.Click += (s, ev) => DoSearch();
            _header.Controls.Add(_btnSearch);

            _userLabel = new Label
            {
                Text = Session.FullName + "\n" + Session.RoleName,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Theme.Fg,
                Font = Theme.Small,
                BackColor = Theme.Surface
            };
            _btnOut = new AnimButton("Выйти", 90, BtnStyle.Secondary) { Height = 38 };
            _btnOut.Click += (s, ev) => { Db.Log("Выход из системы"); Logout = true; Close(); };
            _header.Controls.Add(_userLabel);
            _header.Controls.Add(_btnOut);

            EventHandler place = (s, ev) =>
            {
                int rightMargin = 20;
                _btnOut.Location = new Point(Math.Max(260, _header.Width - _btnOut.Width - rightMargin), 13);
                _userLabel.Location = new Point(Math.Max(240, _btnOut.Left - _userLabel.Width - 14), 14);

                int searchLeft = logo.Right + 24;
                int searchRightLimit = _userLabel.Left - 16;
                int btnW = _btnSearch.Width;
                int gap = 10;
                int availForSearchAndBtn = Math.Max(140, searchRightLimit - searchLeft);
                int searchW = Math.Clamp(availForSearchAndBtn - btnW - gap, 120, 340);

                _search.SetBounds(searchLeft, 18, searchW, _search.Height);
                _btnSearch.Location = new Point(_search.Right + gap, 13);
            };
            _header.Resize += place;
            place(null, EventArgs.Empty);

            Controls.Add(_header);
        }

        // ---------------- хлебные крошки ----------------
        void BuildCrumbs()
        {
            _crumbBar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Theme.Bg };
            _crumbs = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                WrapContents = false,
                Padding = new Padding(20, 8, 0, 0)
            };
            _crumbBar.Controls.Add(_crumbs);
            Controls.Add(_crumbBar);
        }

        void SetCrumbs(List<(string text, Action click)> items)
        {
            _crumbs.SuspendLayout();
            foreach (Control c in _crumbs.Controls.Cast<Control>().ToList())
            {
                _crumbs.Controls.Remove(c);
                c.Dispose();
            }
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0)
                    _crumbs.Controls.Add(new Label
                    {
                        Text = "›",
                        AutoSize = true,
                        ForeColor = Theme.Muted,
                        Margin = new Padding(0, 0, 6, 0)
                    });
                var it = items[i];
                var lb = new Label
                {
                    Text = it.text,
                    AutoSize = true,
                    Margin = new Padding(0, 0, 6, 0),
                    ForeColor = it.click != null ? Theme.Accent : Theme.Fg,
                    Font = it.click != null ? Theme.Body : Theme.BodyBold,
                    Cursor = it.click != null ? Cursors.Hand : Cursors.Default
                };
                if (it.click != null)
                {
                    var act = it.click;
                    lb.Click += (s, ev) => act();
                }
                _crumbs.Controls.Add(lb);
            }
            _crumbs.ResumeLayout();
        }

        // ---------------- боковое меню ----------------
        void BuildSidebar()
        {
            _side = new Panel { Dock = DockStyle.Left, Width = 210, BackColor = Theme.Surface, AutoScroll = true };

            int y = 12;
            Action<string, string> add = (key, text) =>
            {
                var b = new AnimButton(text, 190, BtnStyle.Ghost) { Location = new Point(10, y), Height = 34 };
                b.Click += (s, ev) => Go(key);
                _side.Controls.Add(b);
                _nav[key] = b;
                y += 38;
            };

            add("home", "Главная");
            add("clients", "Клиенты");
            add("shoots", "Съёмки");
            add("orders", "Заказы (печать)");
            add("payments", "Оплата");
            add("calc", "Калькулятор");
            add("reports", "Отчёты");

            if (Session.IsAdmin)
            {
                y += 6;
                _side.Controls.Add(Theme.Lbl("АДМИНИСТРИРОВАНИЕ", 16, y, Theme.Small, Theme.Muted));
                y += 22;
                add("services", "Услуги");
                add("formats", "Форматы печати");
                add("photographers", "Фотографы");
                add("users", "Пользователи");
                add("log", "Журнал действий");
            }

            int lastMenuBottom = y + 12;
            _aboutBtn = new AnimButton("О программе / Контакты", 190, BtnStyle.Secondary) { Height = 36 };
            _aboutBtn.Click += (s, ev) =>
            {
                using (var f = new AboutForm()) f.ShowDialog(this);
            };
            _side.Controls.Add(_aboutBtn);

            EventHandler placeAbout = (s, ev) =>
            {
                int targetY = Math.Max(lastMenuBottom, _side.ClientSize.Height - 48);
                _aboutBtn.Location = new Point(10, targetY);
            };
            _side.Resize += placeAbout;
            placeAbout(null, EventArgs.Empty);

            Controls.Add(_side);
        }

        void SetActive(string key)
        {
            foreach (var kv in _nav)
            {
                bool on = kv.Key == key;
                kv.Value.BaseColor = on ? Theme.Surface2 : Theme.Surface;
                kv.Value.TextColor = on ? Theme.Accent : Theme.Fg;
                kv.Value.Invalidate();
            }
        }

        // ---------------- подвал ----------------
        void BuildFooter()
        {
            _footer = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.Surface };
            _footer.Controls.Add(Theme.Lbl(
                "© " + DateTime.Now.Year + " " + AppInfo.Name + "   •   Вы вошли как: " + Session.FullName + " (" + Session.RoleName + ")",
                14, 6, Theme.Small, Theme.Muted));
            _clock = new Label { AutoSize = true, ForeColor = Theme.Muted, Font = Theme.Small, BackColor = Theme.Surface };
            _footer.Controls.Add(_clock);
            Action upd = () =>
            {
                _clock.Text = DateTime.Now.ToString("dd.MM.yyyy  HH:mm:ss");
                _clock.Location = new Point(Math.Max(14, _footer.Width - _clock.Width - 14), 6);
            };
            _tick = new System.Windows.Forms.Timer { Interval = 1000 };
            _tick.Tick += (s, ev) => upd();
            _footer.Resize += (s, ev) => upd();
            _tick.Start();
            upd();
            Controls.Add(_footer);
        }

        // ---------------- навигация ----------------
        public void Go(string key)
        {
            Action home = () => Go("home");
            var crumbs = new List<(string text, Action click)>();

            switch (key)
            {
                case "home":
                    crumbs.Add(("Главная", null));
                    Open(new HomePage(Go), key, crumbs);
                    return;
                case "calc":
                    crumbs.Add(("Главная", home));
                    crumbs.Add(("Калькулятор стоимости", null));
                    Open(new CalcPage(), key, crumbs);
                    return;
                case "reports":
                    crumbs.Add(("Главная", home));
                    crumbs.Add(("Отчёты и резервное копирование", null));
                    Open(new ReportsPage(), key, crumbs);
                    return;
                default:
                    var ent = Entities.Get(key);
                    if (ent == null) return;
                    if (ent.AdminOnly && !Session.IsAdmin)
                    {
                        MessageBox.Show("Раздел доступен только администратору.", "Доступ запрещён", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    crumbs.Add(("Главная", home));
                    if (ent.AdminOnly) crumbs.Add(("Администрирование", null));
                    crumbs.Add((ent.Title, null));
                    Open(new EntityPage(ent), key, crumbs);
                    return;
            }
        }

        void DoSearch()
        {
            string q = _search.Text.Trim();
            if (q.Length < 2)
            {
                MessageBox.Show("Введите минимум 2 символа для поиска.", "Поиск", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var crumbs = new List<(string text, Action click)>
            {
                ("Главная", () => Go("home")),
                ("Поиск: " + q, null)
            };
            Open(new SearchPage(q, Go), null, crumbs);
        }

        /// <summary>Показывает страницу с плавным переходом: снимок старой страницы перетекает в снимок новой
        /// (затухание + лёгкий сдвиг справа). Сама страница появляется уже готовой, без перерисовки на каждом кадре.</summary>
        void Open(Control page, string activeKey, List<(string text, Action click)> crumbs)
        {
            int id = ++_navId;
            bool first = _current == null;
            _current = page;

            int w = _content.ClientSize.Width, h = _content.ClientSize.Height;
            bool animate = !first && w > 8 && h > 8 && _content.IsHandleCreated && Visible;

            // 1) снимок того, что на экране сейчас (вместе с незавершённым переходом, если он идёт)
            Bitmap from = null;
            if (animate)
            {
                try
                {
                    from = new Bitmap(w, h);
                    _content.DrawToBitmap(from, new Rectangle(0, 0, w, h));
                }
                catch { if (from != null) from.Dispose(); from = null; animate = false; }
            }

            // 2) убираем старое содержимое
            foreach (Control c in _content.Controls.Cast<Control>().ToList())
            {
                _content.Controls.Remove(c);
                c.Dispose();
            }

            // 3) ставим новую страницу и обновляем меню / крошки
            page.Dock = DockStyle.Fill;
            _content.Controls.Add(page);
            SetActive(activeKey);
            SetCrumbs(crumbs);

            if (!animate) { if (from != null) from.Dispose(); return; }

            // 4) снимок новой страницы
            Bitmap to = null;
            try
            {
                _content.PerformLayout();
                page.SetBounds(0, 0, w, h);
                page.PerformLayout();
                to = new Bitmap(w, h);
                page.DrawToBitmap(to, new Rectangle(0, 0, w, h));
            }
            catch
            {
                if (from != null) from.Dispose();
                if (to != null) to.Dispose();
                return;
            }

            // 5) плавный переход
            var curtain = new PageCurtain(from, to) { Dock = DockStyle.Fill };
            _content.Controls.Add(curtain);
            curtain.BringToFront();

            Anim.Run(340,
                p => { if (id == _navId && !curtain.IsDisposed) curtain.Progress = Anim.EaseOut(p); },
                () =>
                {
                    if (curtain.IsDisposed) return;
                    _content.Controls.Remove(curtain);
                    curtain.Dispose();
                },
                false,
                curtain);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F)) { _search.Focus(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}