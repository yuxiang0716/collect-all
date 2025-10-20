using BCrypt.Net;

namespace collect_all.Services
{
    public static class PasswordService
    {
        public static string CreatePasswordHash(string password)
            => BCrypt.Net.BCrypt.HashPassword(password);

        public static bool VerifyPasswordHash(string password, string storedHash)
            => !string.IsNullOrEmpty(storedHash) && BCrypt.Net.BCrypt.Verify(password, storedHash);
    }
}
