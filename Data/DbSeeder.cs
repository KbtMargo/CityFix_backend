using CityFix_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Data
{
    public static class DbSeeder
    {
        public const string AdminEmail = "admin@cityfix.local";
        public const string AdminPassword = "Admin123!";

        // Creates the first Administrator if there is none yet. Safe to run on every start.
        public static async Task SeedAdminAsync(IServiceProvider services, ILogger logger)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (await db.Users.AnyAsync(u => u.Role == UserRole.Administrator))
            {
                return;
            }

            if (await db.Users.AnyAsync(u => u.Email.ToLower() == AdminEmail))
            {
                logger.LogWarning("No Administrator exists, but {Email} is taken by another user. Change that user's role in the database.", AdminEmail);
                return;
            }

            var now = DateTime.UtcNow;
            db.Users.Add(new User
            {
                Email = AdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword),
                FirstName = "Admin",
                Role = UserRole.Administrator,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();

            logger.LogWarning("Seeded Administrator account: {Email} / {Password} - change this password after the first login.", AdminEmail, AdminPassword);
        }
    }
}
