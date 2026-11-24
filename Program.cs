using System;
using System.Globalization;
using System.Windows.Forms;

namespace PhotoStudio
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
            CultureInfo.CurrentUICulture = new CultureInfo("ru-RU");

            Application.ThreadException += (s, ev) =>
                MessageBox.Show(ev.Exception.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);

            try
            {
                Db.Init();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось подготовить базу данных:\n" + ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            while (true)
            {
                using (var login = new LoginForm())
                {
                    if (login.ShowDialog() != DialogResult.OK) break;
                }
                using (var main = new MainForm())
                {
                    main.ShowDialog();
                    if (!main.Logout) break;
                }
            }
        }
    }
}
