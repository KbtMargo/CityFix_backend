using System.Security.Claims;
using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public interface IAuthService
    {
        // An optional association code: the residents' code makes a Resident of that association,
        // the head code its head. Without a code the account is a Resident with no association.
        Task<RegisterResult> RegisterAsync(RegisterRequest request);

        // The user when the email and password match and the account is not blocked, otherwise null.
        Task<User?> LoginAsync(string email, string password);

        // False when the current password is wrong.
        Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);

        ClaimsPrincipal CreatePrincipal(User user);

        // Checks a session cookie against the database on every request: null when the account is gone
        // or blocked (sign out), otherwise a principal with the current role and name.
        Task<ClaimsPrincipal?> RefreshPrincipalAsync(ClaimsPrincipal principal);
    }
}
