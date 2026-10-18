using System.Security.Claims;
using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public record RegisterRequest(
        string Email,
        string Password,
        string FirstName,
        string? LastName,
        string? Phone,
        string? InvitationCode);

    // Either the created user, or field-level errors (field name → localization key).
    public record RegisterResult(User? User, IReadOnlyDictionary<string, string> Errors)
    {
        public bool Succeeded => User != null;
    }

    public interface IAuthService
    {
        Task<RegisterResult> RegisterAsync(RegisterRequest request);

        // Returns the user when the email and password match, otherwise null.
        Task<User?> LoginAsync(string email, string password);

        // False when the current password is wrong.
        Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);

        ClaimsPrincipal CreatePrincipal(User user);

        string HashPassword(string password);

        string GenerateTemporaryPassword();

        string NormalizeEmail(string email);
    }
}
