using System.Security.Claims;
using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public interface IAuthService
    {
        Task<RegisterResult> RegisterAsync(RegisterRequest request);

        Task<LoginResult> LoginAsync(string email, string password);

        string CreateEmailToken(int userId, string email);

        Task SendConfirmationAsync(User user, string email, string link);

        Task<(EmailConfirmation Result, User? User)> ConfirmEmailAsync(string token);

        Task<User?> FindUnconfirmedAsync(string email);

        Task<User?> ChangePasswordAsync(int userId, string currentPassword, string newPassword);

        string CreatePasswordResetToken(User user);

        Task<User?> FindByResetTokenAsync(string token);

        Task<bool> ResetPasswordAsync(string token, string newPassword);

        Task<User?> FindForPasswordResetAsync(string email);

        Task SendPasswordResetAsync(User user, string link);

        Task SendAccountExistsAsync(User user, string resetLink);

        Task<bool> VerifyPasswordAsync(int userId, string password);

        ClaimsPrincipal CreatePrincipal(User user);

        Task<ClaimsPrincipal?> RefreshPrincipalAsync(ClaimsPrincipal principal);
    }
}
