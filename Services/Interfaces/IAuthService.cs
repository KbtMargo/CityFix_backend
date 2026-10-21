using System.Security.Claims;
using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public interface IAuthService
    {
        Task<RegisterResult> RegisterAsync(RegisterRequest request);

        Task<User?> LoginAsync(string email, string password);

        string CreateEmailToken(int userId, string email);

        Task SendConfirmationAsync(User user, string email, string link);

        Task<(EmailConfirmation Result, User? User)> ConfirmEmailAsync(string token);

        Task<User?> FindUnconfirmedAsync(string email);

        Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);

        ClaimsPrincipal CreatePrincipal(User user);

        Task<ClaimsPrincipal?> RefreshPrincipalAsync(ClaimsPrincipal principal);
    }
}
