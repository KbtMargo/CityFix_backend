using System.Security.Claims;
using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public interface IAuthService
    {
        // Creates an unconfirmed account (replacing an earlier unconfirmed one with the same email).
        // An optional association code: the residents' code makes a Resident of that association,
        // the head code its head. Without a code the account is a Resident with no association.
        Task<RegisterResult> RegisterAsync(RegisterRequest request);

        // The user when the email and password match and the account is not blocked, otherwise null.
        // The caller still checks EmailConfirmed.
        Task<User?> LoginAsync(string email, string password);

        // A signed link token for this account and address, valid for Email:ConfirmationLifetime.
        string CreateEmailToken(int userId, string email);

        // The confirmation letter (in the current UI language) with the given link.
        Task SendConfirmationAsync(User user, string email, string link);

        // Confirms the registration address, or switches the login to the new address typed in the profile.
        Task<(EmailConfirmation Result, User? User)> ConfirmEmailAsync(string token);

        // For "send the letter again": the account with this email that still waits for confirmation.
        Task<User?> FindUnconfirmedAsync(string email);

        // False when the current password is wrong.
        Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);

        ClaimsPrincipal CreatePrincipal(User user);

        // Checks a session cookie against the database on every request: null when the account is gone
        // or blocked (sign out), otherwise a principal with the current role and name.
        Task<ClaimsPrincipal?> RefreshPrincipalAsync(ClaimsPrincipal principal);
    }
}
