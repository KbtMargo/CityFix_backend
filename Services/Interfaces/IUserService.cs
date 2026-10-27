using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public interface IUserService
    {
        Task<User> GetActorAsync(int userId);

        Task<User?> FindActorAsync(int userId);

        Task<UsersPage> GetPageAsync(User actor, string? q, int page);

        Task<User> GetAsync(int id);

        Task<(User User, string TemporaryPassword)> CreateAsync(UserInput input, User actor);

        Task UpdateAsync(int id, UserInput input, User actor);

        Task<string> ResetPasswordAsync(int id, User actor);

        Task SetBlockedAsync(int id, bool blocked, User actor);

        Task<DeleteCheck> CheckDeleteAsync(int id, User actor);

        Task DeleteAsync(int id, User actor);

        Task<string?> UpdateProfileAsync(int userId, ProfileInput input);

        Task UpdateNotificationSettingsAsync(int userId, NotificationSettings settings);

        Task SetLanguageAsync(int userId, string language);

        Task<PersonalDataExport> ExportPersonalDataAsync(int userId);

        Task AnonymizeAsync(int userId);

        Task<User> CreateAccountAsync(UserInput input, string password, bool emailConfirmed, string language = "uk", bool mustChangePassword = false);
    }
}
