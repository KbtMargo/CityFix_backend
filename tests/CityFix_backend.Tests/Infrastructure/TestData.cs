using CityFix_backend.Data;
using CityFix_backend.Models;
using CityFix_backend.Services;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Tests.Infrastructure;

/// <summary>Creates rows straight in the database. Every e-mail and code is unique, so tests sharing a database do not collide.</summary>
public sealed class TestData(ApplicationDbContext db, DateTime now)
{
    public const string Password = "Passw0rd!";

    // Cost 4 keeps the suite fast; BCrypt.Verify reads the cost from the hash.
    public static readonly string PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4);

    public const double Lat = 49.42161;
    public const double Lng = 26.99653;

    public static string UniqueEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@test.local";

    // Detached, so a service working on the same DbContext loads its own copy and the test keeps the original values.
    private async Task<T> SaveAsync<T>(T entity) where T : class
    {
        db.Add(entity);
        await db.SaveChangesAsync();
        db.Entry(entity).State = EntityState.Detached;
        return entity;
    }

    public async Task<Acab> AcabAsync(string? name = null)
    {
        var acab = new Acab
        {
            Name = name ?? "ОСББ " + Guid.NewGuid().ToString("N")[..6],
            Address = "вул. Тестова, 1",
            ResidentCode = RandomCodes.InvitationCode(),
            HeadCode = RandomCodes.InvitationCode(),
            CreatedAt = now,
            UpdatedAt = now
        };
        return await SaveAsync(acab);
    }

    public async Task<User> UserAsync(UserRole role = UserRole.Resident, Acab? acab = null, string? email = null,
        bool confirmed = true, string firstName = "Тарас", string? lastName = "Шевченко", bool blocked = false, bool mustChangePassword = false)
    {
        var user = new User
        {
            Email = EmailAddress.Normalize(email ?? UniqueEmail(role.ToString().ToLowerInvariant())),
            PasswordHash = PasswordHash,
            EmailConfirmed = confirmed,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            AcabId = acab?.Id,
            IsBlocked = blocked,
            MustChangePassword = mustChangePassword,
            CreatedAt = now,
            UpdatedAt = now
        };
        return await SaveAsync(user);
    }

    public Task<User> AdminAsync() => UserAsync(UserRole.Administrator, firstName: "Адмін");

    public Task<User> HeadAsync(Acab acab) => UserAsync(UserRole.AcabAdmin, acab, firstName: "Голова");

    public async Task<Category> CategoryAsync(string? name = null, string? nameEn = "Roads")
    {
        var category = new Category { Name = name ?? "Дороги " + Guid.NewGuid().ToString("N")[..6], NameEn = nameEn };
        return await SaveAsync(category);
    }

    public async Task<Recipient> RecipientAsync(string? name = null)
    {
        var recipient = new Recipient { Name = name ?? "Водоканал " + Guid.NewGuid().ToString("N")[..6], NameEn = "Water utility", ContactInfo = "0382 000 000" };
        return await SaveAsync(recipient);
    }

    public async Task<Appeal> AppealAsync(User author, Category category, AppealStatus status = AppealStatus.New,
        double? lat = Lat, double? lng = Lng, string? title = null, DateTime? createdAt = null, Recipient? recipient = null,
        AppealPriority priority = AppealPriority.Normal, DateTime? dueAt = null, DateTime? closedAt = null, string? address = "вул. Тестова, 1")
    {
        var created = createdAt ?? now;
        var appeal = new Appeal
        {
            Title = title ?? "Яма на дорозі " + Guid.NewGuid().ToString("N")[..6],
            Description = "Глибока яма біля під'їзду",
            Lat = lat == null ? null : (decimal)lat,
            Lng = lng == null ? null : (decimal)lng,
            Address = address,
            Status = status,
            Priority = priority,
            AuthorId = author.Id,
            AcabId = author.AcabId,
            CategoryId = category.Id,
            ResponsibleKind = recipient == null && author.AcabId != null ? ResponsibleKind.Association : ResponsibleKind.Recipient,
            RecipientId = recipient?.Id,
            CreatedAt = created,
            UpdatedAt = created,
            DueAt = dueAt ?? created.AddDays(7),
            ClosedAt = closedAt ?? (AppealWorkflow.Closed.Contains(status) ? now : null)
        };
        return await SaveAsync(appeal);
    }

    /// <summary>A typical neighbourhood: an association with a head and two residents, plus a category and a city service.</summary>
    public async Task<Neighbourhood> NeighbourhoodAsync()
    {
        var acab = await AcabAsync();
        return new Neighbourhood(
            acab,
            await HeadAsync(acab),
            await UserAsync(UserRole.Resident, acab, firstName: "Олена"),
            await UserAsync(UserRole.Resident, acab, firstName: "Петро"),
            await CategoryAsync(),
            await RecipientAsync());
    }
}

public record Neighbourhood(Acab Acab, User Head, User Resident, User Neighbour, Category Category, Recipient Recipient);
