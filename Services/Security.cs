using System.Security.Cryptography;

namespace CityFix_backend.Services
{
    public static class EmailAddress
    {
        public static string Normalize(string? email) => (email ?? "").Trim().ToLowerInvariant();
    }

    public static class PhoneNumber
    {
        public static string? ToE164(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
            if (phone.TrimStart().StartsWith('+')) return digits.Length is >= 8 and <= 15 ? "+" + digits : null;
            if (digits.Length == 10 && digits[0] == '0') return "+38" + digits;
            if (digits.Length == 12 && digits.StartsWith("380")) return "+" + digits;
            return null;
        }
    }

    public interface IPasswordHasher
    {
        string Hash(string password);

        bool Verify(string password, string hash);

        void VerifyDummy(string password);
    }

    public class BCryptPasswordHasher : IPasswordHasher
    {
        private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("cityfix-timing-guard");

        public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

        public bool Verify(string password, string hash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return false;
            }
        }

        public void VerifyDummy(string password) => BCrypt.Net.BCrypt.Verify(password, DummyHash);
    }

    public static class RandomCodes
    {
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const string PasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

        public static string InvitationCode() => RandomNumberGenerator.GetString(Alphabet, 10);

        public static string TemporaryPassword() => RandomNumberGenerator.GetString(PasswordAlphabet, 12);

        public static string NormalizeCode(string? code) => (code ?? "").Replace(" ", "").Trim().ToUpperInvariant();
    }
}
