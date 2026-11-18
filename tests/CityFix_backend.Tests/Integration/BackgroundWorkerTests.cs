using CityFix_backend.Data;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CityFix_backend.Tests.Integration;

[Trait("Category", "Integration")]
public class BackgroundWorkerTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>, IAsyncLifetime
{
    private ServiceHarness _app = null!;

    public Task InitializeAsync()
    {
        _app = new ServiceHarness(database);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private static async Task Eventually(Func<Task<bool>> condition, int timeoutMs = 15_000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!await condition())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("The background worker did not finish in time.");
            await Task.Delay(100);
        }
    }

    private async Task RunAsync(IHostedService worker, Func<Task<bool>> until)
    {
        await worker.StartAsync(CancellationToken.None);
        try { await Eventually(until); }
        finally { await worker.StopAsync(CancellationToken.None); }
    }

    private Task<T> ReadAsync<T>(Func<ApplicationDbContext, Task<T>> read) =>
        _app.InScopeAsync(sp => read(sp.GetRequiredService<ApplicationDbContext>()));

    private NotificationDispatcher Dispatcher(int maxAttempts = 5) => new(
        _app.Root.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new NotificationOptions { DispatchInterval = TimeSpan.FromMilliseconds(100), MaxAttempts = maxAttempts, PublicUrl = "https://cityfix.test/" }),
        NullLogger<NotificationDispatcher>.Instance);

    private async Task<Notification> PendingAsync(User user, Appeal appeal, NotificationType type = NotificationType.StatusChanged, AppealStatus? status = AppealStatus.Done)
    {
        var note = new Notification
        {
            UserId = user.Id, AppealId = appeal.Id, Type = type, NewStatus = status, Message = "Яму залатано",
            EmailState = DeliveryState.Pending, CreatedAt = DateTime.UtcNow, NextAttemptAt = DateTime.UtcNow.AddMinutes(-1)
        };
        _app.Db.Notifications.Add(note);
        await _app.Db.SaveChangesAsync();
        return note;
    }

    private Task<Notification> NoteAsync(int id) => ReadAsync(db => db.Notifications.AsNoTracking().SingleAsync(n => n.Id == id));

    [Fact]
    public async Task Dispatcher_emails_pending_notifications_in_the_users_language()
    {
        var user = await _app.Data.UserAsync(firstName: "Olena");
        await _app.Get<CityFix_backend.Services.Interfaces.IUserService>().SetLanguageAsync(user.Id, "en");
        var appeal = await _app.Data.AppealAsync(user, await _app.Data.CategoryAsync(), title: "Pothole on Main street");
        var note = await PendingAsync(user, appeal);

        await RunAsync(Dispatcher(), async () => (await NoteAsync(note.Id)).EmailState == DeliveryState.Sent);

        var mail = Assert.Single(_app.Email.To(user.Email));
        Assert.Contains("Pothole on Main street", mail.Message.Subject + mail.Message.Text);
        Assert.Contains("Яму залатано", mail.Message.Text);
        Assert.Equal($"https://cityfix.test/Appeals?appealId={appeal.Id}", mail.Link);
        Assert.Contains("Olena", mail.Message.Html);
    }

    [Fact]
    public async Task Dispatcher_gives_up_after_the_last_attempt()
    {
        var user = await _app.Data.UserAsync();
        var note = await PendingAsync(user, await _app.Data.AppealAsync(user, await _app.Data.CategoryAsync()));
        _app.Email.Fail = true;

        await RunAsync(Dispatcher(maxAttempts: 1), async () => (await NoteAsync(note.Id)).EmailState == DeliveryState.Failed);

        Assert.Equal(1, (await NoteAsync(note.Id)).Attempts);
    }

    [Fact]
    public async Task Dispatcher_schedules_a_retry_after_a_failure()
    {
        var user = await _app.Data.UserAsync();
        var note = await PendingAsync(user, await _app.Data.AppealAsync(user, await _app.Data.CategoryAsync()));
        _app.Email.Fail = true;

        await RunAsync(Dispatcher(maxAttempts: 5), async () => (await NoteAsync(note.Id)).Attempts == 1);

        var stored = await NoteAsync(note.Id);
        Assert.Equal(DeliveryState.Pending, stored.EmailState);
        Assert.True(stored.NextAttemptAt > DateTime.UtcNow.AddSeconds(20));
    }

    [Fact]
    public async Task Dispatcher_skips_blocked_and_opted_out_users()
    {
        var blocked = await _app.Data.UserAsync(blocked: true);
        var optedOut = await _app.Data.UserAsync();
        await _app.Get<CityFix_backend.Services.Interfaces.IUserService>().UpdateNotificationSettingsAsync(optedOut.Id, new NotificationSettings(true, false));
        var category = await _app.Data.CategoryAsync();
        var first = await PendingAsync(blocked, await _app.Data.AppealAsync(blocked, category));
        var second = await PendingAsync(optedOut, await _app.Data.AppealAsync(optedOut, category));

        await RunAsync(Dispatcher(), async () =>
            (await NoteAsync(first.Id)).EmailState == DeliveryState.None && (await NoteAsync(second.Id)).EmailState == DeliveryState.None);

        Assert.Empty(_app.Email.To(blocked.Email));
        Assert.Empty(_app.Email.To(optedOut.Email));
    }

    private TranslationWorker Translator() => new(
        _app.Root.GetRequiredService<IServiceScopeFactory>(),
        _app.Root.GetRequiredService<IOptions<TranslationOptions>>(),
        NullLogger<TranslationWorker>.Instance);

    [Fact]
    public async Task Translator_fills_in_appeals_names_and_addresses()
    {
        var category = await _app.Data.CategoryAsync("Освітлення " + Guid.NewGuid().ToString("N")[..4], nameEn: null);
        var user = await _app.Data.UserAsync(firstName: "Юрій", lastName: "Коваль");
        var appeal = await _app.Data.AppealAsync(user, category, title: "Не світить ліхтар", address: "вул. Зарічанська, 10");
        await _app.Db.Appeals.Where(a => a.Id == appeal.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.TranslationPending, true));

        await RunAsync(Translator(), () => ReadAsync(db => db.Appeals.AnyAsync(a => a.Id == appeal.Id && !a.TranslationPending && a.AddressTranslated != null)));

        var stored = await ReadAsync(db => db.Appeals.AsNoTracking().SingleAsync(a => a.Id == appeal.Id));
        Assert.Equal(("[en] Не світить ліхтар", "[en] Глибока яма біля під'їзду", "[en] вул. Зарічанська, 10"), (stored.TitleTranslated, stored.DescriptionTranslated, stored.AddressTranslated));
        Assert.Equal($"[en] {category.Name}", (await ReadAsync(db => db.Categories.AsNoTracking().SingleAsync(c => c.Id == category.Id))).NameEn);
        Assert.Equal("Yurii Koval", (await ReadAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id))).FullNameTranslated);
    }

    [Fact]
    public async Task Translator_stops_retrying_an_appeal_after_the_attempt_limit()
    {
        var user = await _app.Data.UserAsync();
        var appeal = await _app.Data.AppealAsync(user, await _app.Data.CategoryAsync());
        await _app.Db.Appeals.Where(a => a.Id == appeal.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.TranslationPending, true));
        _app.Translator.Fail = true;

        await RunAsync(Translator(), () => ReadAsync(db => db.Appeals.AnyAsync(a => a.Id == appeal.Id && !a.TranslationPending)));

        var stored = await ReadAsync(db => db.Appeals.AsNoTracking().SingleAsync(a => a.Id == appeal.Id));
        Assert.Equal((2, (string?)null), (stored.TranslationAttempts, stored.TitleTranslated));
    }

    [Fact]
    public async Task Disabled_translation_does_nothing()
    {
        var worker = new TranslationWorker(_app.Root.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new TranslationOptions { Enabled = false }), NullLogger<TranslationWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(300);
        await worker.StopAsync(CancellationToken.None);
        Assert.Empty(_app.Translator.Calls);
    }

    [Fact]
    public async Task Deadline_worker_reports_overdue_appeals_on_start()
    {
        var n = await _app.Data.NeighbourhoodAsync();
        var appeal = await _app.Data.AppealAsync(n.Resident, n.Category, AppealStatus.InProgress, dueAt: _app.Clock.GetUtcNow().UtcDateTime.AddDays(-1));
        var worker = new DeadlineWorker(_app.Root.GetRequiredService<IServiceScopeFactory>(), NullLogger<DeadlineWorker>.Instance);

        await RunAsync(worker, () => ReadAsync(db => db.Appeals.AnyAsync(a => a.Id == appeal.Id && a.OverdueNotifiedAt != null)));

        Assert.True(await ReadAsync(db => db.Notifications.AnyAsync(x => x.AppealId == appeal.Id && x.UserId == n.Head.Id && x.Type == NotificationType.Overdue)));
    }
}

[Trait("Category", "Integration")]
public class AdminSeederTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    private ServiceHarness Harness(string? password, string environment = "Development", string email = "Admin@CityFix.Test") => new(_database, services =>
    {
        services.Configure<SeedOptions>(o => { o.AdminEmail = email; o.AdminPassword = password; });
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(new TestWebHostEnvironment(Path.GetTempPath()) { EnvironmentName = environment });
    });

    [Fact]
    public async Task First_start_creates_one_administrator_who_must_change_the_password()
    {
        await using var app = Harness("FirstPassw0rd");

        await DbSeeder.SeedAdminAsync(app.Root, NullLogger.Instance);
        await DbSeeder.SeedAdminAsync(app.Root, NullLogger.Instance);

        var admin = Assert.Single(await app.Db.Users.Where(u => u.Role == UserRole.Administrator).ToListAsync());
        Assert.Equal(("admin@cityfix.test", true, true), (admin.Email, admin.MustChangePassword, admin.EmailConfirmed));
        Assert.NotNull((await app.Get<CityFix_backend.Services.Interfaces.IAuthService>().LoginAsync("admin@cityfix.test", "FirstPassw0rd")).User);
    }

    [Fact]
    public async Task Production_refuses_to_invent_an_administrator_password()
    {
        await using var app = Harness(null, "Production");
        await Assert.ThrowsAsync<InvalidOperationException>(() => DbSeeder.SeedAdminAsync(app.Root, NullLogger.Instance));
    }

    [Fact]
    public async Task Development_generates_a_password_when_none_is_configured()
    {
        await using var app = Harness(null);
        await DbSeeder.SeedAdminAsync(app.Root, NullLogger.Instance);
        Assert.True(await app.Db.Users.AnyAsync(u => u.Role == UserRole.Administrator));
    }

    [Fact]
    public async Task Existing_resident_with_the_admin_email_is_not_promoted()
    {
        await using var app = Harness("x", email: "taken@cityfix.test");
        await app.Data.UserAsync(email: "taken@cityfix.test");
        await DbSeeder.SeedAdminAsync(app.Root, NullLogger.Instance);
        Assert.False(await app.Db.Users.AnyAsync(u => u.Role == UserRole.Administrator));
    }
}
