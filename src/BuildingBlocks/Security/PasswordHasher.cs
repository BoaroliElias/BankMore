using System.Security.Cryptography;
using System.Text;

namespace BuildingBlocks.Security
{
    public static class PasswordHasher
    {
        private const int SaltSizeBytes = 16;      // 128 bits
        private const int HashSizeBytes = 32;      // 256 bits
        private const int Iterations = 100_000; // custo

        public static (string HashBase64, string SaltBase64) Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
            var hash = Pbkdf2(password, salt, Iterations, HashSizeBytes);
            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }

        public static bool Verify(string password, string saltBase64, string hashBase64)
        {
            var salt = Convert.FromBase64String(saltBase64);
            var stored = Convert.FromBase64String(hashBase64);

            // PBKDF2
            var pbkdf2 = Pbkdf2(password, salt, Iterations, HashSizeBytes);
            if (CryptographicOperations.FixedTimeEquals(stored, pbkdf2))
                return true;

            // Fallback legado SHA256("senha:salBase64")
            var legacy = SHA256.HashData(Encoding.UTF8.GetBytes($"{password}:{saltBase64}"));
            return CryptographicOperations.FixedTimeEquals(stored, legacy);
        }

        private static byte[] Pbkdf2(string password, byte[] salt, int iterations, int length) =>
            new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256).GetBytes(length);
    }
}
