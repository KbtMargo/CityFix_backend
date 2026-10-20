using CityFix_backend.Models;
using CityFix_backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CityFix_backend.Data
{
    public static class DbSeeder
    {
        // Creates the first Administrator if there is none yet. Safe to run on every start.
        // The password comes from Seed:AdminPassword (user secrets or an environment variable); without it
        // a random one is generated and written to the log this one time, since nobody could sign in otherwise.
        public static async Task SeedAdminAsync(IServiceProvider services, ILogger logger)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var options = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
            var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

            if (await db.Users.AnyAsync(u => u.Role == UserRole.Administrator))
            {
                return;
            }

            var email = EmailAddress.Normalize(options.AdminEmail);
            if (await db.Users.AnyAsync(u => u.Email == email))
            {
                logger.LogWarning("No Administrator exists, but {Email} is taken by another user. Change that user's role in the database.", email);
                return;
            }

            var generated = string.IsNullOrWhiteSpace(options.AdminPassword);
            var password = generated ? RandomCodes.TemporaryPassword() : options.AdminPassword!;
            db.Users.Add(new User
            {
                Email = email,
                PasswordHash = hasher.Hash(password),
                EmailConfirmed = true,
                FirstName = "Admin",
                Role = UserRole.Administrator,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();

            if (generated)
            {
                logger.LogWarning("Seeded Administrator {Email} with the one-time password {Password}. Change it after the first sign-in.", email, password);
            }
            else
            {
                logger.LogInformation("Seeded Administrator {Email} with the configured password.", email);
            }
        }
    }
}
