using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace PhotoStudio
{
    /// <summary>Универсальное сохранение/удаление записей по описанию Entity.</summary>
    public static class Repo
    {
        public static void Save(Entity e, long? id, Dictionary<string, object> v)
        {
            var items = new List<(string col, object val)>();
            foreach (var f in e.Fields)
            {
                if (f.Type == FT.Password)
                {
                    string pwd = v[f.Col] as string;
                    if (string.IsNullOrEmpty(pwd)) continue;   // при редактировании пустой пароль = не менять
                    var hs = Security.Hash(pwd);
                    items.Add(("PasswordHash", hs.hash));
                    items.Add(("Salt", hs.salt));
                }
                else
                {
                    items.Add((f.Col, v[f.Col]));
                }
            }

            if (id == null)
            {
                foreach (var x in e.InsertExtra) items.Add((x.Col, x.Val()));
                var cols = new List<string>();
                var phs = new List<string>();
                var ps = new List<(string, object)>();
                for (int i = 0; i < items.Count; i++)
                {
                    cols.Add(items[i].col);
                    phs.Add("@p" + i);
                    ps.Add(("@p" + i, items[i].val));
                }
                Db.Exec("INSERT INTO " + e.Table + "(" + string.Join(",", cols) + ") VALUES(" + string.Join(",", phs) + ")", ps.ToArray());
                Db.Log("Добавлена запись: " + e.Title);
            }
            else
            {
                var sets = new List<string>();
                var ps = new List<(string, object)>();
                for (int i = 0; i < items.Count; i++)
                {
                    sets.Add(items[i].col + "=@p" + i);
                    ps.Add(("@p" + i, items[i].val));
                }
                ps.Add(("@id", id.Value));
                Db.Exec("UPDATE " + e.Table + " SET " + string.Join(",", sets) + " WHERE Id=@id", ps.ToArray());
                Db.Log("Изменена запись №" + id + ": " + e.Title);
            }
        }

        public static bool Delete(Entity e, long id, out string error)
        {
            error = null;
            try
            {
                Db.Exec("DELETE FROM " + e.Table + " WHERE Id=@id", ("@id", id));
                Db.Log("Удалена запись №" + id + ": " + e.Title);
                return true;
            }
            catch (SqliteException ex)
            {
                error = Explain(ex);
                return false;
            }
        }

        public static string Explain(SqliteException ex)
        {
            switch (ex.SqliteExtendedErrorCode)
            {
                case 2067:
                case 1555:
                    return "Такая запись уже существует (нарушена уникальность).";
                case 787:
                    return "Операция невозможна: есть связанные записи в других таблицах.";
                case 275:
                    return "Значение не прошло проверку ограничений базы данных.";
                case 1299:
                    return "Не заполнено обязательное поле.";
                default:
                    return ex.Message;
            }
        }
    }
}
