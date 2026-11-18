using CityFix_backend.Models;
using CityFix_backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Tests.Integration;

[Trait("Category", "Integration")]
public class DatabaseSchemaTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>
{
    private async Task<List<string>> ScalarsAsync(string sql)
    {
        await using var db = database.CreateContext();
        return await db.Database.SqlQueryRaw<string>(sql).ToListAsync();
    }

    [Fact]
    public async Task All_migrations_apply_to_an_empty_database()
    {
        await using var db = database.CreateContext();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Model_has_no_changes_missing_from_the_migrations()
    {
        await using var db = database.CreateContext();
        Assert.False(db.Database.HasPendingModelChanges(), "Run `dotnet ef migrations add` — the model differs from the last migration.");
    }

    [Fact]
    public async Task PostGIS_is_installed()
    {
        Assert.Contains("postgis", await ScalarsAsync("SELECT extname AS \"Value\" FROM pg_extension"));
    }

    [Fact]
    public async Task Every_entity_has_its_table()
    {
        var tables = await ScalarsAsync("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'");
        Assert.Superset(new HashSet<string> { "Users", "Acabs", "Categories", "Recipients", "Appeals", "AppealUpdates", "AppealFiles",
            "Notifications", "AuditLog", "AppealSupports", "__EFMigrationsHistory" }, tables.ToHashSet());
    }

    [Fact]
    public async Task Location_is_computed_from_the_coordinates()
    {
        await using var db = database.CreateContext();
        var data = new TestData(db, DateTime.UtcNow);
        var appeal = await data.AppealAsync(await data.UserAsync(), await data.CategoryAsync(), lat: 49.5, lng: 27.25);

        db.ChangeTracker.Clear();
        var stored = await db.Appeals.SingleAsync(a => a.Id == appeal.Id);
        Assert.NotNull(stored.Location);
        Assert.Equal((27.25, 49.5, 4326), (stored.Location!.X, stored.Location.Y, stored.Location.SRID));

        var noPoint = await data.AppealAsync(await data.UserAsync(), await data.CategoryAsync(), lat: null, lng: null);
        db.ChangeTracker.Clear();
        Assert.Null((await db.Appeals.SingleAsync(a => a.Id == noPoint.Id)).Location);
    }

    [Fact]
    public async Task Spatial_and_lookup_indexes_exist()
    {
        var indexes = await ScalarsAsync("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'");
        Assert.Contains(indexes, i => i.Contains("\"Appeals\"") && i.Contains("USING gist") && i.Contains("\"Location\""));
        Assert.Contains(indexes, i => i.Contains("UNIQUE") && i.Contains("\"Users\"") && i.Contains("\"Email\""));
        Assert.Contains(indexes, i => i.Contains("UNIQUE") && i.Contains("\"HeadCode\""));
        Assert.Contains(indexes, i => i.Contains("\"Appeals\"") && i.Contains("\"DueAt\""));
    }

    [Fact]
    public async Task Duplicate_emails_are_refused_by_the_database()
    {
        await using var db = database.CreateContext();
        var data = new TestData(db, DateTime.UtcNow);
        var email = TestData.UniqueEmail();
        await data.UserAsync(email: email);
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<DbUpdateException>(() => new TestData(db, DateTime.UtcNow).UserAsync(email: email));
    }

    [Fact]
    public async Task Deleting_an_appeal_cascades_to_its_history_files_supports_and_notifications()
    {
        await using var db = database.CreateContext();
        var now = DateTime.UtcNow;
        var data = new TestData(db, now);
        var author = await data.UserAsync();
        var supporter = await data.UserAsync();
        var appeal = await data.AppealAsync(author, await data.CategoryAsync());
        db.AppealUpdates.Add(new AppealUpdate { AppealId = appeal.Id, ChangedById = author.Id, ChangedAt = now });
        db.AppealFiles.Add(new AppealFile { AppealId = appeal.Id, FileName = "a.png", FilePath = "uploads/a.png", ContentType = "image/png", UploadedAt = now });
        db.AppealSupports.Add(new AppealSupport { AppealId = appeal.Id, UserId = supporter.Id, CreatedAt = now });
        db.Notifications.Add(new Notification { AppealId = appeal.Id, UserId = author.Id, CreatedAt = now, NextAttemptAt = now });
        await db.SaveChangesAsync();

        await db.Appeals.Where(a => a.Id == appeal.Id).ExecuteDeleteAsync();

        Assert.False(await db.AppealUpdates.AnyAsync(u => u.AppealId == appeal.Id));
        Assert.False(await db.AppealFiles.AnyAsync(f => f.AppealId == appeal.Id));
        Assert.False(await db.AppealSupports.AnyAsync(s => s.AppealId == appeal.Id));
        Assert.False(await db.Notifications.AnyAsync(n => n.AppealId == appeal.Id));
    }

    [Fact]
    public async Task Users_with_appeals_cannot_be_deleted_at_database_level()
    {
        await using var db = database.CreateContext();
        var data = new TestData(db, DateTime.UtcNow);
        var author = await data.UserAsync();
        await data.AppealAsync(author, await data.CategoryAsync());
        await Assert.ThrowsAnyAsync<Exception>(() => db.Users.Where(u => u.Id == author.Id).ExecuteDeleteAsync());
    }
}
