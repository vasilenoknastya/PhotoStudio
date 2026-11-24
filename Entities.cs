using System;
using System.Linq;

namespace PhotoStudio
{
    /// <summary>Описание всех страниц с данными: запросы, поля форм, валидация, права доступа.</summary>
    public static class Entities
    {
        const string PersonRx = @"^[\p{L}\-\.' ]{5,}$";
        const string PhoneRx = @"^\+?[\d\s\-\(\)]{9,20}$";

        public static readonly Entity Clients = new Entity
        {
            Key = "clients", Title = "Клиенты", Table = "Clients", Sort = "[ФИО]",
            SelectSql = """
                SELECT c.Id, c.FullName AS "ФИО", c.Phone AS "Телефон", c.Email AS "Email",
                       c.Notes AS "Заметки", c.RegisteredAt AS "Регистрация",
                       (SELECT COUNT(*) FROM Shoots s WHERE s.ClientId=c.Id) AS "Съёмок"
                FROM Clients c
                """,
            Fields = new[]
            {
                new Field("FullName", "ФИО", FT.Text).Len(100).Rx(PersonRx, "Введите ФИО буквами (минимум 5 символов)"),
                new Field("Phone", "Телефон", FT.Text).Len(25).Rx(PhoneRx, "Формат: +375 (29) 123-45-67"),
                new Field("Email", "Email", FT.Text).Opt().Len(80).Rx(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", "Некорректный email"),
                new Field("Notes", "Заметки", FT.Multiline).Opt().Len(300)
            },
            InsertExtra = new (string, Func<object>)[] { ("RegisteredAt", () => Db.Now()) }
        };

        public static readonly Entity Shoots = new Entity
        {
            Key = "shoots", Title = "Съёмки", Table = "Shoots", Sort = "[Дата и время] DESC", FilterColumn = "Статус",
            SelectSql = """
                SELECT sh.Id, c.FullName AS "Клиент", p.FullName AS "Фотограф", sv.Name AS "Услуга",
                       sh.ShootDate AS "Дата и время", sh.Hours AS "Часов", sh.Location AS "Место",
                       sh.Status AS "Статус", ROUND(sv.PricePerHour*sh.Hours,2) AS "Стоимость"
                FROM Shoots sh
                JOIN Clients c ON c.Id=sh.ClientId
                JOIN Photographers p ON p.Id=sh.PhotographerId
                JOIN Services sv ON sv.Id=sh.ServiceId
                """,
            Fields = new[]
            {
                new Field("ClientId", "Клиент", FT.Combo).Look("SELECT Id, FullName AS Name FROM Clients ORDER BY FullName"),
                new Field("PhotographerId", "Фотограф", FT.Combo).Look("SELECT Id, FullName AS Name FROM Photographers ORDER BY FullName"),
                new Field("ServiceId", "Услуга", FT.Combo).Look("SELECT Id, Name||' — '||PricePerHour||' BYN/ч' AS Name FROM Services ORDER BY Name"),
                new Field("ShootDate", "Дата и время съёмки", FT.Moment),
                new Field("Hours", "Длительность, ч", FT.Decimal).Range(0.5m, 12m, 1),
                new Field("Location", "Место съёмки", FT.Text).Opt().Len(80),
                new Field("Status", "Статус", FT.Combo).Choice("Запланирована", "Проведена", "Отменена").Def("Запланирована")
            }
        };

        public static readonly Entity Orders = new Entity
        {
            Key = "orders", Title = "Заказы (печать)", Table = "Orders", Sort = "Id DESC", FilterColumn = "Статус",
            SelectSql = """
                SELECT t.*, ROUND(t."Сумма"-t."Оплачено",2) AS "Долг" FROM (
                    SELECT o.Id, c.FullName AS "Клиент", sv.Name AS "Услуга", sh.ShootDate AS "Съёмка",
                           pf.Name AS "Формат печати", o.Quantity AS "Кол-во", o.Status AS "Статус",
                           ROUND(sv.PricePerHour*sh.Hours+pf.Price*o.Quantity,2) AS "Сумма",
                           ROUND(COALESCE((SELECT SUM(p.Amount) FROM Payments p WHERE p.OrderId=o.Id),0),2) AS "Оплачено"
                    FROM Orders o
                    JOIN Shoots sh ON sh.Id=o.ShootId
                    JOIN Clients c ON c.Id=sh.ClientId
                    JOIN Services sv ON sv.Id=sh.ServiceId
                    JOIN PrintFormats pf ON pf.Id=o.PrintFormatId) t
                """,
            Fields = new[]
            {
                new Field("ShootId", "Съёмка (один заказ на съёмку)", FT.Combo).Look(
                    "SELECT sh.Id, c.FullName||' — '||sv.Name||' — '||sh.ShootDate AS Name FROM Shoots sh JOIN Clients c ON c.Id=sh.ClientId JOIN Services sv ON sv.Id=sh.ServiceId ORDER BY sh.ShootDate DESC"),
                new Field("PrintFormatId", "Формат печати", FT.Combo).Look("SELECT Id, Name||' — '||Price||' BYN' AS Name FROM PrintFormats ORDER BY Price"),
                new Field("Quantity", "Количество отпечатков", FT.Int).Range(0, 1000, 0),
                new Field("Status", "Статус заказа", FT.Combo).Choice("Новый", "В печати", "Готов", "Выдан").Def("Новый")
            },
            InsertExtra = new (string, Func<object>)[] { ("CreatedAt", () => Db.Now()) },
            Validate = (v, id) =>
            {
                string st = Db.Scalar("SELECT Status FROM Shoots WHERE Id=@i", ("@i", v["ShootId"])) as string;
                if (st == "Отменена") return "Нельзя оформить заказ на отменённую съёмку.";
                return null;
            }
        };

        public static readonly Entity Payments = new Entity
        {
            Key = "payments", Title = "Оплата", Table = "Payments", Sort = "[Дата оплаты] DESC", FilterColumn = "Способ",
            SelectSql = """
                SELECT p.Id, p.OrderId AS "№ заказа", c.FullName AS "Клиент", p.Amount AS "Сумма, BYN",
                       p.Method AS "Способ", p.PaidAt AS "Дата оплаты"
                FROM Payments p
                JOIN Orders o ON o.Id=p.OrderId
                JOIN Shoots sh ON sh.Id=o.ShootId
                JOIN Clients c ON c.Id=sh.ClientId
                """,
            Fields = new[]
            {
                new Field("OrderId", "Заказ", FT.Combo).Look(
                    "SELECT o.Id, '№'||o.Id||' — '||c.FullName||' ('||sh.ShootDate||')' AS Name FROM Orders o JOIN Shoots sh ON sh.Id=o.ShootId JOIN Clients c ON c.Id=sh.ClientId ORDER BY o.Id DESC"),
                new Field("Amount", "Сумма платежа, BYN", FT.Decimal).Range(0.01m, 100000m),
                new Field("Method", "Способ оплаты", FT.Combo).Choice("Наличные", "Карта", "Перевод").Def("Карта"),
                new Field("PaidAt", "Дата и время оплаты", FT.Moment)
            },
            Validate = (v, id) =>
            {
                var dt = Db.Query("""
                    SELECT ROUND(sv.PricePerHour*sh.Hours+pf.Price*o.Quantity,2) AS Total,
                           ROUND(COALESCE((SELECT SUM(p.Amount) FROM Payments p WHERE p.OrderId=o.Id AND p.Id<>@pid),0),2) AS Paid
                    FROM Orders o
                    JOIN Shoots sh ON sh.Id=o.ShootId
                    JOIN Services sv ON sv.Id=sh.ServiceId
                    JOIN PrintFormats pf ON pf.Id=o.PrintFormatId
                    WHERE o.Id=@oid
                    """, ("@oid", v["OrderId"]), ("@pid", id ?? -1L));
                if (dt.Rows.Count == 0) return "Заказ не найден.";
                double left = Convert.ToDouble(dt.Rows[0]["Total"]) - Convert.ToDouble(dt.Rows[0]["Paid"]);
                double amt = Convert.ToDouble(v["Amount"]);
                if (amt > left + 0.005) return $"Сумма платежа больше остатка по заказу ({left:N2} BYN).";
                return null;
            }
        };

        // ---------- администрирование ----------
        public static readonly Entity Services = new Entity
        {
            Key = "services", Title = "Услуги съёмки", Table = "Services", Sort = "[Название]", AdminOnly = true,
            SelectSql = """SELECT Id, Name AS "Название", PricePerHour AS "Цена за час, BYN" FROM Services""",
            Fields = new[]
            {
                new Field("Name", "Название услуги", FT.Text).Len(60),
                new Field("PricePerHour", "Цена за час, BYN", FT.Decimal).Range(1m, 10000m)
            }
        };

        public static readonly Entity Formats = new Entity
        {
            Key = "formats", Title = "Форматы печати", Table = "PrintFormats", Sort = "[Формат]", AdminOnly = true,
            SelectSql = """SELECT Id, Name AS "Формат", Price AS "Цена за шт., BYN" FROM PrintFormats""",
            Fields = new[]
            {
                new Field("Name", "Название формата", FT.Text).Len(60),
                new Field("Price", "Цена за штуку, BYN", FT.Decimal).Range(0.1m, 10000m)
            }
        };

        public static readonly Entity Photographers = new Entity
        {
            Key = "photographers", Title = "Фотографы", Table = "Photographers", Sort = "[ФИО]", AdminOnly = true,
            SelectSql = """
                SELECT ph.Id, ph.FullName AS "ФИО", ph.Specialty AS "Специализация", ph.Phone AS "Телефон",
                       (SELECT COUNT(*) FROM Shoots s WHERE s.PhotographerId=ph.Id) AS "Съёмок"
                FROM Photographers ph
                """,
            Fields = new[]
            {
                new Field("FullName", "ФИО", FT.Text).Len(100).Rx(PersonRx, "Введите ФИО буквами (минимум 5 символов)"),
                new Field("Specialty", "Специализация", FT.Text).Len(80),
                new Field("Phone", "Телефон", FT.Text).Len(25).Rx(PhoneRx, "Формат: +375 (29) 123-45-67")
            }
        };

        public static readonly Entity Users = new Entity
        {
            Key = "users", Title = "Пользователи", Table = "Users", Sort = "[Логин]", AdminOnly = true, FilterColumn = "Роль",
            SelectSql = """SELECT Id, Login AS "Логин", FullName AS "ФИО", Role AS "Роль" FROM Users""",
            Fields = new[]
            {
                new Field("Login", "Логин", FT.Text).Len(20).Rx(@"^[A-Za-z0-9_]{3,20}$", "3–20 символов: латиница, цифры, «_»"),
                new Field("FullName", "ФИО", FT.Text).Len(100),
                new Field("Role", "Роль", FT.Combo).Choice("Admin", "User").Def("User"),
                new Field("Password", "Пароль (при правке пусто = не менять)", FT.Password).Len(50)
            },
            Validate = (v, id) =>
            {
                if (id != null && id.Value == Session.UserId && (string)v["Role"] != "Admin")
                    return "Нельзя снять права администратора у самого себя.";
                return null;
            }
        };

        public static readonly Entity Log = new Entity
        {
            Key = "log", Title = "Журнал действий", Table = "ActionLog", Sort = "Id DESC", AdminOnly = true, ReadOnly = true,
            SelectSql = """SELECT Id, At AS "Время", UserLogin AS "Пользователь", Action AS "Действие" FROM ActionLog""",
            FilterColumn = "Пользователь"
        };

        public static readonly Entity[] All = { Clients, Shoots, Orders, Payments, Services, Formats, Photographers, Users, Log };

        public static Entity Get(string key)
        {
            return All.FirstOrDefault(x => x.Key == key);
        }
    }
}
