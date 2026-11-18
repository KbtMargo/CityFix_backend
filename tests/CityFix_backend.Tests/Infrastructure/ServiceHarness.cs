using CityFix_backend.Data;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace CityFix_backend.Tests.Infrastructure;

/// <summary>
/// The application's services wired as in Program.cs, against a test database, with a controllable clock and
/// fake e-mail, geocoding and translation.
/// </summary>
public sealed class ServiceHarness : IAsyncDisposable
{
    private readonly ServiceProvider _root;
    private IServiceScope _scope;

    public FakeTimeProvider Clock { get; }
    public FakeEmailSender Email { get; } = new();
    public FakeGeocoding Geocoding { get; } = new();
    public FakeTranslator Translator { get; } = new();
    public string ContentRoot { get; } = TestFiles.TempFolder();

    public ServiceHarness(PostgresDatabase database, Action<IServiceCollection>? configure = null, DateTimeOffset? now = null)
    {
        // Whole seconds: PostgreSQL keeps microseconds, so sub-tick values would not round-trip.
        var start = now ?? DateTimeOffset.UtcNow;
        Clock = new FakeTimeProvider(new DateTimeOffset(start.UtcTicks - start.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero));
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(database.ConnectionString, n => n.UseNetTopologySuite()));
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddMemoryCache();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddHttpContextAccessor();
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(new TestWebHostEnvironment(ContentRoot));
        services.AddSingleton<IEmailSender>(Email);
        services.AddSingleton<IGeocodingService>(Geocoding);
        services.AddSingleton<ITranslator>(Translator);
        services.AddOptions<AppealOptions>();
        services.AddOptions<UploadOptions>();
        services.AddOptions<EmailOptions>();
        services.AddOptions<SeedOptions>();
        services.Configure<NotificationOptions>(o => o.DispatchInterval = TimeSpan.FromMilliseconds(100));
        services.Configure<TranslationOptions>(o =>
        {
            o.Interval = TimeSpan.FromMilliseconds(100);
            o.RetryAfterFailure = TimeSpan.FromMilliseconds(100);
            o.MaxAttempts = 2;
        });

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAcabService, AcabService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAppealService, AppealService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IAuditLog, AuditLog>();
        configure?.Invoke(services);

        _root = services.BuildServiceProvider();
        _scope = _root.CreateScope();
    }

    public IServiceProvider Root => _root;

    public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public ApplicationDbContext Db => Get<ApplicationDbContext>();

    public TestData Data => new(Db, Clock.GetUtcNow().UtcDateTime);

    public T Option<T>() where T : class => _root.GetRequiredService<IOptions<T>>().Value;

    /// <summary>Starts a fresh scope (a new DbContext), as the next HTTP request would get.</summary>
    public void NewScope()
    {
        _scope.Dispose();
        _scope = _root.CreateScope();
    }

    /// <summary>Runs code in a separate scope, e.g. to check what another request sees.</summary>
    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> run)
    {
        using var scope = _root.CreateScope();
        return await run(scope.ServiceProvider);
    }

    public async ValueTask DisposeAsync()
    {
        _scope.Dispose();
        await _root.DisposeAsync();
        try { Directory.Delete(ContentRoot, recursive: true); } catch (IOException) { }
    }
}
