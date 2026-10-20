using System.Security.Cryptography;

namespace CityFix_backend.Services
{
    // Emails are compared and stored in one canonical form.
    public static class EmailAddress
    {
        public static string Normalize(string? email) => (email ?? "").Trim().ToLowerInvariant();
    }

    // Phones are typed freely ("+380 67 123-45-67", "067 123 45 67"); SMS providers want E.164.
    public static class PhoneNumber
    {
        // +380XXXXXXXXX for a Ukrainian number written without the country code; null when it is not a number.
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

        // False for a wrong password or a value that is not a BCrypt hash.
        bool Verify(string password, string hash);

        // Spends the same time as a real check, so "no such user" cannot be told apart by timing.
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
                // Not a BCrypt hash (rows typed in before hashing existed): this account cannot log in.
                return false;
            }
        }

        public void VerifyDummy(string password) => BCrypt.Net.BCrypt.Verify(password, DummyHash);
    }

    // Random strings people read out or copy by hand: no 0/O or 1/l/I.
    public static class RandomCodes
    {
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const string PasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

        // 10 characters of a 32-letter alphabet: 2^50 combinations, with registration rate-limited.
        public static string InvitationCode() => RandomNumberGenerator.GetString(Alphabet, 10);

        public static string TemporaryPassword() => RandomNumberGenerator.GetString(PasswordAlphabet, 12);

        // Codes are typed case-insensitively and with or without spaces.
        public static string NormalizeCode(string? code) => (code ?? "").Replace(" ", "").Trim().ToUpperInvariant();
    }
}
