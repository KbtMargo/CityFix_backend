using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    // Accounts: the signed-in user, the users table, profiles, blocking.
    public interface IUserService
    {
        // The signed-in user as stored now (role and association are read from the database).
        // ForbiddenException if the account is gone or blocked.
        Task<User> GetActorAsync(int userId);

        // Administrators see everyone; a head sees the members of their association. q searches name and email.
        Task<UsersPage> GetPageAsync(User actor, string? q, int page);

        Task<User> GetAsync(int id);

        // Admin "Add user": returns the user and a one-time temporary password.
        Task<(User User, string TemporaryPassword)> CreateAsync(UserInput input, User actor);

        Task UpdateAsync(int id, UserInput input, User actor);

        // A new one-time temporary password for an existing account.
        Task<string> ResetPasswordAsync(int id, User actor);

        Task SetBlockedAsync(int id, bool blocked, User actor);

        Task<DeleteCheck> CheckDeleteAsync(int id, User actor);

        Task DeleteAsync(int id, User actor);

        // The user's own name, email and phone. A changed email is kept as pending until confirmed:
        // returns that address when a confirmation letter must be sent, otherwise null.
        Task<string?> UpdateProfileAsync(int userId, ProfileInput input);

        // Profile → Notifications.
        Task UpdateNotificationSettingsAsync(int userId, NotificationSettings settings);

        // The language of the user's emails and SMS ("uk" / "en").
        Task SetLanguageAsync(int userId, string language);

        // Shared by registration and "Add user": checks the email and creates the account.
        Task<User> CreateAccountAsync(UserInput input, string password, bool emailConfirmed, string language = "uk");
    }
}
