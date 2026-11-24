using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace PhotoStudio
{
    /// <summary>Окно входа: логин и пароль, проверка по хэшу, анимированный фон, «тряска» при ошибке, блокировка после 5 неудач.</summary>
    public class LoginForm : Form
    {
        readonly TextBox _login = new TextBox();
        readonly TextBox _pass = new TextBox();
        readonly Label _err = new Label();
        readonly AnimButton _btn;
        readonly Panel _card = new Panel();
        int _fails;
        int _finalTop;

        public LoginForm()
        {
            Text = AppInfo.Name + " — вход";
            ClientSize = new Size(920, 580);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = Theme.Body;
            BackColor = Theme.Bg;
            ForeColor = Theme.Fg;

            var bg = new BokehPanel { Dock = DockStyle.Fill };
            Controls.Add(bg);

            // Логотип и подпись рисуются прямо на анимированном фоне (а не Label-ами с прозрачным фоном,
            // которые давали тёмные прямоугольники и заезжали на карточку входа)
            bg.PaintOverlay = g =>
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (var titleBrush = new SolidBrush(Theme.Accent))
                using (var subBrush = new SolidBrush(Theme.Fg))
                using (var subFont = new Font("Georgia", 14f, FontStyle.Italic))
                {
                    float maxW = Math.Max(200, _card.Left - 50 - 30);
                    float size = 34f;
                    Font titleFont = new Font("Georgia", size, FontStyle.Bold);
                    float w = g.MeasureString("◉ ОБЪЕКТИВ", titleFont).Width;
                    if (w > maxW)
                    {
                        size = size * maxW / w;
                        titleFont.Dispose();
                        titleFont = new Font("Georgia", size, FontStyle.Bold);
                    }
                    g.DrawString("◉ ОБЪЕКТИВ", titleFont, titleBrush, 46, 186);
                    float th = titleFont.GetHeight(g);
                    titleFont.Dispose();
                    g.DrawString("фотостудия\nклиенты  •  съёмки  •  заказы\nпечать  •  оплата", subFont, subBrush, 52, 186 + th + 10);
                }
            };

            _card.Size = new Size(360, 430);
            _card.BackColor = Theme.Surface;
            _finalTop = (ClientSize.Height - _card.Height) / 2;
            _card.Location = new Point(500, _finalTop + 60);
            bg.Controls.Add(_card);

            _card.Controls.Add(Theme.Lbl("Вход в систему", 28, 22, Theme.H2, Theme.Fg));
            _card.Controls.Add(Theme.Lbl("Логин", 28, 80, Theme.Small, Theme.Muted));
            _login.SetBounds(28, 102, 304, 28);
            Theme.StyleInput(_login);
            _card.Controls.Add(_login);

            _card.Controls.Add(Theme.Lbl("Пароль", 28, 146, Theme.Small, Theme.Muted));
            _pass.SetBounds(28, 168, 304, 28);
            _pass.UseSystemPasswordChar = true;
            Theme.StyleInput(_pass);
            _card.Controls.Add(_pass);

            var show = new CheckBox { Text = "Показать пароль", AutoSize = true, ForeColor = Theme.Muted, Location = new Point(28, 206), BackColor = Theme.Surface };
            show.CheckedChanged += (s, ev) => _pass.UseSystemPasswordChar = !show.Checked;
            _card.Controls.Add(show);

            _err.SetBounds(28, 238, 304, 40);
            _err.ForeColor = Theme.Danger;
            _err.BackColor = Theme.Surface;
            _card.Controls.Add(_err);

            _btn = new AnimButton("Войти", 304) { Location = new Point(28, 282), Height = 42 };
            _btn.Click += (s, ev) => DoLogin();
            _card.Controls.Add(_btn);
            AcceptButton = _btn;

            _card.Controls.Add(new Label
            {
                Text = "Демо-доступ:\nadmin / admin123 — администратор\nmanager / user123 — пользователь",
                ForeColor = Theme.Muted, Font = Theme.Small, BackColor = Theme.Surface,
                Location = new Point(28, 340), Size = new Size(304, 70)
            });

            Shown += (s, ev) =>
            {
                _login.Focus();
                Anim.Run(550, p => { if (!_card.IsDisposed) _card.Top = _finalTop + (int)(60 * (1 - p)); });
            };
        }

        void Fail(string message)
        {
            _err.Text = message;
            Anim.Shake(_card);
            if (_fails >= 5) Lock();
        }

        void Lock()
        {
            _btn.Enabled = false;
            int left = 15;
            _err.Text = "Слишком много попыток. Подождите " + left + " с.";
            var t = new System.Windows.Forms.Timer { Interval = 1000 };
            t.Tick += (s, ev) =>
            {
                left--;
                if (IsDisposed) { t.Stop(); t.Dispose(); return; }
                if (left <= 0)
                {
                    t.Stop(); t.Dispose();
                    _btn.Enabled = true; _fails = 0; _err.Text = "";
                }
                else _err.Text = "Слишком много попыток. Подождите " + left + " с.";
            };
            t.Start();
        }

        void DoLogin()
        {
            string l = _login.Text.Trim();
            string p = _pass.Text;
            if (l.Length == 0 || p.Length == 0) { Fail("Введите логин и пароль."); return; }

            var dt = Db.Query("SELECT Id, Login, FullName, Role, PasswordHash, Salt FROM Users WHERE Login=@l COLLATE NOCASE", ("@l", l));
            if (dt.Rows.Count == 0 || !Security.Verify(p, (string)dt.Rows[0]["PasswordHash"], (string)dt.Rows[0]["Salt"]))
            {
                _fails++;
                Db.Log("Неудачная попытка входа: " + l);
                Fail("Неверный логин или пароль.");
                return;
            }

            var r = dt.Rows[0];
            Session.UserId = Convert.ToInt64(r["Id"]);
            Session.Login = (string)r["Login"];
            Session.FullName = (string)r["FullName"];
            Session.Role = (string)r["Role"];
            Db.Log("Вход в систему");
            DialogResult = DialogResult.OK;
        }
    }
}
