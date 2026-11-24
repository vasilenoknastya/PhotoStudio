using System;
using System.Security.Cryptography;

namespace PhotoStudio
{
    /// <summary>Хэширование паролей: PBKDF2-SHA256, 100 000 итераций, уникальная соль на пользователя.</summary>
    public static class Security
    {
        const int Iterations = 100000;

        public static (string hash, string salt) Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] h = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
            return (Convert.ToBase64String(h), Convert.ToBase64String(salt));
        }

        public static bool Verify(string password, string hash, string salt)
        {
            try
            {
                byte[] s = Convert.FromBase64String(salt);
                byte[] expected = Convert.FromBase64String(hash);
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, s, Iterations, HashAlgorithmName.SHA256, 32);
                return CryptographicOperations.FixedTimeEquals(expected, actual);
            }
            catch
            {
                return false;
            }
        }
    }
}
