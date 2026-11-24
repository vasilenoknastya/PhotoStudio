using System;
using System.Collections.Generic;

namespace PhotoStudio
{
    public enum FT { Text, Multiline, Int, Decimal, Moment, Combo, Password }

    /// <summary>Описание поля формы редактирования (тип, обязательность, ограничения).</summary>
    public class Field
    {
        public string Col, Label, Lookup, Pattern, PatternMsg, Default;
        public string[] Options;
        public FT Type;
        public bool Required = true;
        public int MaxLen = 100, Decimals = 2;
        public decimal Min = 0, Max = 1000000;

        public Field(string col, string label, FT type) { Col = col; Label = label; Type = type; }

        public Field Opt() { Required = false; return this; }
        public Field Len(int n) { MaxLen = n; return this; }
        public Field Look(string sql) { Lookup = sql; return this; }
        public Field Choice(params string[] o) { Options = o; return this; }
        public Field Def(string d) { Default = d; return this; }
        public Field Range(decimal min, decimal max, int decimals = 2) { Min = min; Max = max; Decimals = decimals; return this; }
        public Field Rx(string pattern, string msg) { Pattern = pattern; PatternMsg = msg; return this; }
    }

    /// <summary>Описание таблицы-справочника/данных: запрос для сетки, поля для формы, правила доступа.</summary>
    public class Entity
    {
        public string Key, Title, Table, SelectSql, Sort, FilterColumn;
        public bool AdminOnly, ReadOnly;
        public Field[] Fields = new Field[0];
        public (string Col, Func<object> Val)[] InsertExtra = new (string, Func<object>)[0];
        public Func<Dictionary<string, object>, long?, string> Validate;
    }
}
