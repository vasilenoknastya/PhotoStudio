using System;
using System.Drawing;
using System.Windows.Forms;

namespace PhotoStudio
{
    /// <summary>Окно «О программе / Контакты»: данные автора и форма обратной связи (сохраняется в БД).</summary>
    public class AboutForm : Form
    {
        readonly TextBox _msg = new TextBox();
        readonly Label _err = new Label();
        readonly DataGridView _grid = new DataGridView();

        public AboutForm()
        {
            Text = "О программе — " + AppInfo.Name;
            ClientSize = new Size(920, 560);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            ForeColor = Theme.Fg;
            Font = Theme.Body;
            AppIcon.Apply(this);

            var bg = new BokehPanel { Dock = DockStyle.Fill };
            Controls.Add(bg);

            // левая карточка — сведения
            var left = new Panel
            {
                Location = new Point(28, 28),
                Size = new Size(400, 504),
                BackColor = Theme.Surface
            };
            bg.Controls.Add(left);

            left.Controls.Add(Theme.Lbl("◉ ОБЪЕКТИВ", 24, 18, new Font("Georgia", 20f, FontStyle.Bold), Theme.Accent));
            var desc = new Label
            {
                Text = "Информационная система фотостудии: учёт клиентов, съёмок, заказов на печать и оплат.",
                ForeColor = Theme.Fg,
                BackColor = Theme.Surface,
                Location = new Point(24, 62),
                Size = new Size(352, 46)
            };
            left.Controls.Add(desc);

            int y = 118;
            int maxW = 352;
            Action<string, string> info = (k, v) =>
            {
                left.Controls.Add(Theme.Lbl(k, 24, y, Theme.Small, Theme.Muted));

                var lblVal = new Label
                {
                    Text = v ?? "",
                    ForeColor = Theme.Fg,
                    BackColor = Theme.Surface,
                    Font = Theme.BodyBold,
                    Location = new Point(24, y + 16),
                    MaximumSize = new Size(maxW, 0),
                    AutoSize = true
                };
                left.Controls.Add(lblVal);

                int valH = Math.Max(20, lblVal.PreferredHeight);
                y += 16 + valH + 10;
            };

            info("ВЫПОЛНИЛА", AppInfo.Author);
            info("ГРУППА", AppInfo.Group);
            info("ВЕРСИЯ", AppInfo.Version);
            info("EMAIL / ТЕЛЕФОН", AppInfo.Email + "  •  " + AppInfo.Phone);
            info("ТЕХНОЛОГИИ", "C#  •  .NET 8  •  WinForms  •  SQLite");

            var close = new AnimButton("Закрыть", 352, BtnStyle.Secondary)
            {
                Location = new Point(24, Math.Max(y + 8, left.Height - 58)),
                Height = 38
            };
            close.Click += (s, ev) => Close();
            left.Controls.Add(close);

            // правая карточка — обратная связь
            var right = new Panel
            {
                Location = new Point(448, 28),
                Size = new Size(444, 504),
                BackColor = Theme.Surface
            };
            bg.Controls.Add(right);

            right.Controls.Add(Theme.Lbl("Обратная связь", 24, 18, Theme.H2, Theme.Fg));
            right.Controls.Add(Theme.Lbl("Ваше сообщение разработчику", 24, 54, Theme.Small, Theme.Muted));

            _msg.Multiline = true;
            _msg.MaxLength = 500;
            _msg.ScrollBars = ScrollBars.Vertical;
            _msg.SetBounds(24, 74, 396, 84);
            Theme.StyleInput(_msg);
            right.Controls.Add(_msg);

            _err.SetBounds(24, 162, 396, 30);
            _err.ForeColor = Theme.Danger;
            _err.BackColor = Theme.Surface;
            _err.Font = Theme.Small;
            right.Controls.Add(_err);

            var send = new AnimButton("Отправить", 396) { Location = new Point(24, 196), Height = 38 };
            send.Click += (s, ev) => Send();
            right.Controls.Add(send);

            right.Controls.Add(Theme.Lbl("Последние сообщения", 24, 246, Theme.Small, Theme.Muted));
            Theme.StyleGrid(_grid);
            _grid.SetBounds(24, 268, 396, 212);
            right.Controls.Add(_grid);

            LoadFeedback();
            Shown += (s, ev) => Anim.FadeIn(this, 250);
        }

        void LoadFeedback()
        {
            _grid.DataSource = Db.Query("""
                SELECT CreatedAt AS "Дата", UserLogin AS "Автор", Message AS "Сообщение"
                FROM Feedback ORDER BY Id DESC LIMIT 10
                """);
        }

        void Send()
        {
            string m = _msg.Text.Trim();
            if (m.Length < 5)
            {
                _err.Text = "Сообщение слишком короткое (минимум 5 символов).";
                Anim.Shake(_msg);
                return;
            }
            if (m.Length > 500)
            {
                _err.Text = "Максимум 500 символов.";
                Anim.Shake(_msg);
                return;
            }
            _err.Text = "";
            Db.Exec("INSERT INTO Feedback(UserLogin,Message,CreatedAt) VALUES(@u,@m,@t)",
                ("@u", Session.Login), ("@m", m), ("@t", Db.Now()));
            Db.Log("Отправлено сообщение разработчику");
            _msg.Clear();
            LoadFeedback();
        }
    }
}