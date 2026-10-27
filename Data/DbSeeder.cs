using CityFix_backend.Models;
using CityFix_backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CityFix_backend.Data
{
    public static class DbSeeder
    {
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
            if (generated && !scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                // A generated password could only be handed over through the log, which is not a safe place for it.
                throw new InvalidOperationException("No Administrator exists. Set Seed:AdminPassword (user secrets or an environment variable) for the first start.");
            }
            var password = generated ? RandomCodes.TemporaryPassword() : options.AdminPassword!;
            db.Users.Add(new User
            {
                Email = email,
                PasswordHash = hasher.Hash(password),
                MustChangePassword = true,
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
