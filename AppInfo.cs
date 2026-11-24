namespace PhotoStudio
{
    /// <summary>Сведения о приложении и авторе. ЗАМЕНИ значения на свои перед сдачей!</summary>
    public static class AppInfo
    {
        public const string Name = "Фотостудия «Объектив»";
        public const string Author = "Василёнок Анастасия Михайловна";
        public const string Group = "22о";
        public const string Version = "1.0.0";
        public const string Email = "student@example.by";
        public const string Phone = "+375 (29) 000-00-00";
    }

    public static class Session
    {
        public static long UserId;
        public static string Login;
        public static string FullName;
        public static string Role;
        public static bool IsAdmin { get { return Role == "Admin"; } }
        public static string RoleName { get { return IsAdmin ? "Администратор" : "Пользователь"; } }
    }
}
