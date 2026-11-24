using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace PhotoStudio
{
    /// <summary>Работа с БД SQLite: подключение, схема, тестовые данные, журнал, резервная копия.</summary>
    public static class Db
    {
        public static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "Data");
        public static readonly string FilePath = Path.Combine(Dir, "studio.db");
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------- форматы дат (в БД хранятся текстом ISO) ----------
        public static string Fmt(DateTime d) { return d.ToString("yyyy-MM-dd HH:mm", Inv); }
        public static string FmtDate(DateTime d) { return d.ToString("yyyy-MM-dd", Inv); }
        public static string Now() { return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv); }

        public static bool TryParse(string s, out DateTime d)
        {
            return DateTime.TryParseExact(s, new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd" },
                Inv, DateTimeStyles.None, out d);
        }

        // ---------- экранирование для DataView.RowFilter ----------
        public static string EscStr(string s) { return s.Replace("'", "''"); }

        public static string EscLike(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                if (c == '*' || c == '%' || c == '[' || c == ']') sb.Append('[').Append(c).Append(']');
                else if (c == '\'') sb.Append("''");
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // ---------- подключение и запросы ----------
        public static SqliteConnection Open()
        {
            var c = new SqliteConnection("Data Source=" + FilePath);
            c.Open();
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys=ON;";
                cmd.ExecuteNonQuery();
            }
            return c;
        }

        static void Bind(SqliteCommand cmd, (string, object)[] ps)
        {
            foreach (var p in ps) cmd.Parameters.AddWithValue(p.Item1, p.Item2 ?? DBNull.Value);
        }

        public static DataTable Query(string sql, params (string, object)[] ps)
        {
            using (var c = Open())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = sql;
                Bind(cmd, ps);
                using (var r = cmd.ExecuteReader())
                {
                    int n = r.FieldCount;
                    var names = new string[n];
                    for (int i = 0; i < n; i++) names[i] = r.GetName(i);
                    var rows = new List<object[]>();
                    while (r.Read())
                    {
                        var a = new object[n];
                        r.GetValues(a);
                        rows.Add(a);
                    }

                    var dt = new DataTable();
                    var types = new Type[n];
                    for (int i = 0; i < n; i++)
                    {
                        bool anyLong = false, anyDouble = false, other = false;
                        foreach (var row in rows)
                        {
                            object v = row[i];
                            if (v == null || v is DBNull) continue;
                            if (v is long) anyLong = true;
                            else if (v is double) anyDouble = true;
                            else other = true;
                        }
                        types[i] = other ? typeof(string) : anyDouble ? typeof(double) : anyLong ? typeof(long) : typeof(string);
                        dt.Columns.Add(names[i], types[i]);
                    }
                    foreach (var row in rows)
                    {
                        var vals = new object[n];
                        for (int i = 0; i < n; i++)
                        {
                            object v = row[i];
                            if (v == null || v is DBNull) vals[i] = DBNull.Value;
                            else if (types[i] == typeof(double)) vals[i] = Convert.ToDouble(v, Inv);
                            else if (types[i] == typeof(string) && !(v is string)) vals[i] = Convert.ToString(v, Inv);
                            else vals[i] = v;
                        }
                        dt.Rows.Add(vals);
                    }
                    return dt;
                }
            }
        }

        public static object Scalar(string sql, params (string, object)[] ps)
        {
            using (var c = Open())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = sql;
                Bind(cmd, ps);
                object o = cmd.ExecuteScalar();
                return o is DBNull ? null : o;
            }
        }

        public static int Exec(string sql, params (string, object)[] ps)
        {
            using (var c = Open())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = sql;
                Bind(cmd, ps);
                return cmd.ExecuteNonQuery();
            }
        }

        // ---------- журнал и резервное копирование ----------
        public static void Log(string action)
        {
            try
            {
                Exec("INSERT INTO ActionLog(UserLogin,Action,At) VALUES(@u,@a,@t)",
                    ("@u", Session.Login ?? "system"), ("@a", action), ("@t", Now()));
            }
            catch { }
        }

        public static void Backup(string destPath)
        {
            if (File.Exists(destPath)) File.Delete(destPath);
            using (var src = Open())
            using (var dst = new SqliteConnection("Data Source=" + destPath))
            {
                dst.Open();
                src.BackupDatabase(dst);
            }
            SqliteConnection.ClearAllPools();
        }

        // ---------- схема ----------
        const string Schema = """
            CREATE TABLE IF NOT EXISTS Users(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Login TEXT NOT NULL UNIQUE,
                FullName TEXT NOT NULL,
                Role TEXT NOT NULL CHECK(Role IN ('Admin','User')),
                PasswordHash TEXT NOT NULL,
                Salt TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Clients(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FullName TEXT NOT NULL,
                Phone TEXT NOT NULL,
                Email TEXT,
                Notes TEXT,
                RegisteredAt TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Photographers(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FullName TEXT NOT NULL,
                Specialty TEXT NOT NULL,
                Phone TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Services(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                PricePerHour REAL NOT NULL CHECK(PricePerHour>0));
            CREATE TABLE IF NOT EXISTS PrintFormats(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                Price REAL NOT NULL CHECK(Price>0));
            CREATE TABLE IF NOT EXISTS Shoots(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ClientId INTEGER NOT NULL REFERENCES Clients(Id),
                PhotographerId INTEGER NOT NULL REFERENCES Photographers(Id),
                ServiceId INTEGER NOT NULL REFERENCES Services(Id),
                ShootDate TEXT NOT NULL,
                Hours REAL NOT NULL CHECK(Hours>0),
                Location TEXT,
                Status TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Orders(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ShootId INTEGER NOT NULL UNIQUE REFERENCES Shoots(Id),
                PrintFormatId INTEGER NOT NULL REFERENCES PrintFormats(Id),
                Quantity INTEGER NOT NULL CHECK(Quantity>=0),
                Status TEXT NOT NULL,
                CreatedAt TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Payments(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderId INTEGER NOT NULL REFERENCES Orders(Id),
                Amount REAL NOT NULL CHECK(Amount>0),
                Method TEXT NOT NULL,
                PaidAt TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS ActionLog(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserLogin TEXT NOT NULL,
                Action TEXT NOT NULL,
                At TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Feedback(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserLogin TEXT NOT NULL,
                Message TEXT NOT NULL,
                CreatedAt TEXT NOT NULL);
            """;

        public static void Init()
        {
            Directory.CreateDirectory(Dir);
            using (var c = Open())
            {
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = Schema;
                    cmd.ExecuteNonQuery();
                }
                long n;
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Users";
                    n = Convert.ToInt64(cmd.ExecuteScalar());
                }
                if (n == 0) Seed(c);
            }
        }

        // ---------- тестовые данные ----------
        static void Seed(SqliteConnection c)
        {
            var rnd = new Random(42);
            using (var tx = c.BeginTransaction())
            {
                void X(string sql, params (string, object)[] ps)
                {
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = sql;
                        Bind(cmd, ps);
                        cmd.ExecuteNonQuery();
                    }
                }

                void U(string login, string name, string role, string pwd)
                {
                    var hs = Security.Hash(pwd);
                    X("INSERT INTO Users(Login,FullName,Role,PasswordHash,Salt) VALUES(@a,@b,@c,@d,@e)",
                        ("@a", login), ("@b", name), ("@c", role), ("@d", hs.hash), ("@e", hs.salt));
                }

                U("admin", "Администратор студии", "Admin", "admin123");
                U("manager", "Менеджер Анна Белова", "User", "user123");

                // Фотографы
                var phs = new[]
                {
                    ("Соколов Артём Игоревич", "Свадебная и репортажная", "+375 (29) 111-22-33"),
                    ("Крылова Мария Андреевна", "Портрет и Love Story", "+375 (29) 222-33-44"),
                    ("Власов Денис Павлович", "Предметная и коммерческая", "+375 (33) 333-44-55"),
                    ("Литвинова Яна Олеговна", "Семейная и детская", "+375 (44) 444-55-66"),
                    ("Громов Павел Сергеевич", "Корпоративная съёмка", "+375 (25) 555-66-77")
                };
                foreach (var p in phs)
                    X("INSERT INTO Photographers(FullName,Specialty,Phone) VALUES(@a,@b,@c)",
                        ("@a", p.Item1), ("@b", p.Item2), ("@c", p.Item3));

                // Услуги (цена за час, BYN)
                var svc = new (string n, double p)[]
                {
                    ("Портретная съёмка", 60), ("Семейная съёмка", 70), ("Свадебная съёмка", 120),
                    ("Love Story", 80), ("Предметная съёмка", 50), ("Корпоративная съёмка", 90), ("Детская съёмка", 65)
                };
                foreach (var s in svc)
                    X("INSERT INTO Services(Name,PricePerHour) VALUES(@a,@b)", ("@a", s.n), ("@b", s.p));

                // Форматы печати (цена за штуку, BYN)
                var fmt = new (string n, double p)[]
                {
                    ("10×15 см", 0.8), ("15×21 см", 2.5), ("20×30 см (A4)", 5), ("30×40 см (A3)", 11),
                    ("Фотокнига 20×20", 45), ("Холст 40×60", 55)
                };
                foreach (var f in fmt)
                    X("INSERT INTO PrintFormats(Name,Price) VALUES(@a,@b)", ("@a", f.n), ("@b", f.p));

                // Клиенты (25)
                string[] names =
                {
                    "Иванова Анна Сергеевна", "Петров Дмитрий Алексеевич", "Сидорова Мария Игоревна", "Козлов Андрей Павлович", "Новикова Елена Викторовна",
                    "Морозов Артём Денисович", "Волкова Ольга Николаевна", "Лебедев Максим Олегович", "Семёнова Дарья Андреевна", "Егоров Кирилл Романович",
                    "Павлова Ксения Михайловна", "Зайцев Никита Владимирович", "Орлова Татьяна Юрьевна", "Макаров Илья Станиславович", "Фролова Алина Дмитриевна",
                    "Беляев Роман Евгеньевич", "Тихонова Полина Артёмовна", "Комаров Владислав Иванович", "Гусева Виктория Александровна", "Титов Егор Максимович",
                    "Крылова Наталья Петровна", "Жуков Тимофей Андреевич", "Кузьмина Софья Валерьевна", "Борисов Глеб Николаевич", "Мельникова Вера Леонидовна"
                };
                string[] notes = { "Постоянный клиент", "Пришёл по рекомендации", "Просит съёмку на выходных", "Любит естественный свет", "" };
                for (int i = 0; i < names.Length; i++)
                {
                    string phone = "+375 (" + new[] { "29", "33", "44", "25" }[rnd.Next(4)] + ") " +
                                   rnd.Next(100, 999) + "-" + rnd.Next(10, 99) + "-" + rnd.Next(10, 99);
                    string note = notes[rnd.Next(notes.Length)];
                    X("INSERT INTO Clients(FullName,Phone,Email,Notes,RegisteredAt) VALUES(@a,@b,@c,@d,@e)",
                        ("@a", names[i]), ("@b", phone), ("@c", "client" + (i + 1) + "@example.by"),
                        ("@d", note.Length == 0 ? null : note),
                        ("@e", Fmt(DateTime.Today.AddDays(-rnd.Next(30, 400)))));
                }

                // Съёмки (25)
                double[] hrs = { 1, 1.5, 2, 3, 4 };
                string[] places = { "Студия, зал 1", "Студия, зал 2", "Городской парк", "Выезд к клиенту", "Набережная" };
                var shoots = new List<(int sv, double h, DateTime d, string st)>();
                for (int i = 1; i <= 25; i++)
                {
                    int sv = rnd.Next(svc.Length);
                    double h = hrs[rnd.Next(hrs.Length)];
                    DateTime d = DateTime.Today.AddDays(rnd.Next(-60, 25)).AddHours(rnd.Next(9, 19));
                    string st = d < DateTime.Now ? (rnd.Next(10) == 0 ? "Отменена" : "Проведена") : "Запланирована";
                    shoots.Add((sv, h, d, st));
                    X("INSERT INTO Shoots(ClientId,PhotographerId,ServiceId,ShootDate,Hours,Location,Status) VALUES(@c,@p,@s,@d,@h,@l,@st)",
                        ("@c", i), ("@p", rnd.Next(1, 6)), ("@s", sv + 1), ("@d", Fmt(d)), ("@h", h),
                        ("@l", places[rnd.Next(places.Length)]), ("@st", st));
                }

                // Заказы (25) и оплаты
                string[] methods = { "Наличные", "Карта", "Перевод" };
                string[] printSt = { "В печати", "Готов", "Выдан", "Выдан" };
                for (int i = 1; i <= 25; i++)
                {
                    var s = shoots[i - 1];
                    int f = rnd.Next(fmt.Length);
                    int q = f >= 4 ? rnd.Next(1, 3) : rnd.Next(5, 41);
                    string st = s.st == "Проведена" ? printSt[rnd.Next(printSt.Length)] : "Новый";
                    double total = svc[s.sv].p * s.h + fmt[f].p * q;
                    X("INSERT INTO Orders(ShootId,PrintFormatId,Quantity,Status,CreatedAt) VALUES(@a,@b,@c,@d,@e)",
                        ("@a", i), ("@b", f + 1), ("@c", q), ("@d", st), ("@e", Fmt(s.d)));

                    void Pay(double amount, DateTime when)
                    {
                        if (when > DateTime.Now) when = DateTime.Now.AddHours(-1);
                        X("INSERT INTO Payments(OrderId,Amount,Method,PaidAt) VALUES(@a,@b,@c,@d)",
                            ("@a", i), ("@b", Math.Round(amount, 2)), ("@c", methods[rnd.Next(methods.Length)]), ("@d", Fmt(when)));
                    }

                    if (st == "Выдан")
                    {
                        double first = Math.Round(total * 0.5, 2);
                        Pay(first, s.d.AddDays(-1));
                        Pay(total - first, s.d.AddDays(rnd.Next(2, 8)));
                    }
                    else if (st == "Готов" || st == "В печати")
                    {
                        Pay(total * 0.5, s.d.AddDays(-1));
                    }
                    else if (s.st == "Запланирована" && rnd.Next(10) < 4)
                    {
                        Pay(total * 0.3, DateTime.Now.AddDays(-rnd.Next(1, 4)));
                    }
                }

                // Обратная связь и журнал
                X("INSERT INTO Feedback(UserLogin,Message,CreatedAt) VALUES(@a,@b,@c)",
                    ("@a", "manager"), ("@b", "Отличное приложение, очень удобно искать клиентов!"), ("@c", Now()));
                X("INSERT INTO Feedback(UserLogin,Message,CreatedAt) VALUES(@a,@b,@c)",
                    ("@a", "admin"), ("@b", "Добавить бы экспорт в PDF в следующей версии."), ("@c", Now()));
                X("INSERT INTO ActionLog(UserLogin,Action,At) VALUES(@a,@b,@c)",
                    ("@a", "system"), ("@b", "Создана база данных и загружены тестовые данные"), ("@c", Now()));

                tx.Commit();
            }
        }
    }
}
