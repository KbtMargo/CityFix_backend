using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using CityFix_backend;
using CityFix_backend.Controllers;
using CityFix_backend.Data;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add<ServiceExceptionFilter>();
        options.Filters.Add<PasswordChangeRequiredFilter>();
    })
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));

builder.Services.AddOptions<MvcOptions>().Configure<IStringLocalizerFactory>((options, factory) =>
{
    var localizer = factory.Create(typeof(SharedResource));
    options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => localizer["Validation.Choose"]);
    options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((value, field) => localizer["Validation.Invalid", value, field]);
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), o => o.UseNetTopologySuite()));

// Without a persistent key ring every restart (or container rebuild) signs everyone out and voids e-mail confirmation links.
var keysPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["DataProtection:KeysPath"] ?? "App_Data/keys"));
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("CityFix")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
if (OperatingSystem.IsWindows())
{
    dataProtection.ProtectKeysWithDpapi();
}

var publicUrl = builder.Configuration[$"{NotificationOptions.Section}:{nameof(NotificationOptions.PublicUrl)}"];
if (!builder.Environment.IsDevelopment() && !Uri.TryCreate(publicUrl, UriKind.Absolute, out _))
{
    // Links in e-mails must not be built from the client-supplied Host header.
    throw new InvalidOperationException("Set Notifications:PublicUrl to the site's public address (e.g. https://cityfix.example.org).");
}

// Outside development cookies travel over HTTPS only; locally the plain-http profile keeps working.
var cookieSecurity = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = cookieSecurity);
builder.Services.Configure<CookieTempDataProviderOptions>(options => options.Cookie.SecurePolicy = cookieSecurity);

// Behind a reverse proxy the client address and scheme arrive in X-Forwarded-* headers. Only proxies on this machine
// (the default) or listed in ForwardedHeaders:KnownProxies are trusted, so clients cannot spoof their address.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
    {
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    }
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = cookieSecurity;
        options.Events.OnValidatePrincipal = CookieValidator.ValidateAsync;
    });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy("lookup", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection(UploadOptions.Section));
builder.Services.Configure<AppealOptions>(builder.Configuration.GetSection(AppealOptions.Section));
builder.Services.Configure<GeocodingOptions>(builder.Configuration.GetSection(GeocodingOptions.Section));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.Section));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Section));
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.Section));
builder.Services.Configure<SupportOptions>(builder.Configuration.GetSection(SupportOptions.Section));

builder.Services.Configure<TranslationOptions>(builder.Configuration.GetSection(TranslationOptions.Section));

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
QuestPDF.Settings.UseSystemFonts = true;

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddMemoryCache();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAcabService, AcabService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAppealService, AppealService>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditLog, AuditLog>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddHttpClient<IGeocodingService, NominatimGeocodingService>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<GeocodingOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
});

builder.Services.AddSingleton<GeocodingCache>();

if (string.IsNullOrWhiteSpace(builder.Configuration[$"{EmailOptions.Section}:{nameof(EmailOptions.SmtpHost)}"]))
{
    if (!builder.Environment.IsDevelopment())
    {
        // The log sender would silently drop confirmation letters and write their links into the log.
        throw new InvalidOperationException("Set Email:SmtpHost (and credentials) outside the Development environment.");
    }
    builder.Services.AddSingleton<IEmailSender, LogEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}
builder.Services.AddHostedService<NotificationDispatcher>();
builder.Services.AddHostedService<DeadlineWorker>();

if (string.IsNullOrWhiteSpace(builder.Configuration[$"{TranslationOptions.Section}:{nameof(TranslationOptions.DeepLApiKey)}"]))
{
    builder.Services.AddHttpClient<ITranslator, MyMemoryTranslator>(client => client.Timeout = TimeSpan.FromSeconds(15));
}
else
{
    builder.Services.AddHttpClient<ITranslator, DeepLTranslator>(client => client.Timeout = TimeSpan.FromSeconds(15));
}
builder.Services.AddHostedService<TranslationWorker>();

var app = builder.Build();

app.UseForwardedHeaders();

// Visual Studio's Browser Link and hot reload load a script from, and talk to, the IDE on another localhost port.
// Those sources are allowed in development only.
var devTools = app.Environment.IsDevelopment() ? " http://localhost:* https://localhost:* ws://localhost:* wss://localhost:*" : "";
var contentSecurityPolicy = string.Join("; ",
    "default-src 'self'",
    "script-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com" + devTools,
    "style-src 'self' https://cdnjs.cloudflare.com",
    "img-src 'self' data: blob: https://*.tile.openstreetmap.org https://cdnjs.cloudflare.com",
    "connect-src 'self'" + devTools,
    "font-src 'self'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'");
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.ContentSecurityPolicy = contentSecurityPolicy;
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(self)";
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");

app.UseHttpsRedirection();

app.UseStaticFiles();

var uiCultures = new[] { new CultureInfo("uk"), new CultureInfo("en") };
var localization = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture, uiCultures[0]),
    SupportedCultures = new[] { CultureInfo.InvariantCulture },
    SupportedUICultures = uiCultures,
};
localization.RequestCultureProviders = new List<IRequestCultureProvider> { new CookieRequestCultureProvider() };
app.UseRequestLocalization(localization);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapStaticAssets();

// For load balancers and uptime monitors: plain "Healthy"/"Unhealthy", 503 when the database is down.
app.MapHealthChecks("/health").DisableRateLimiting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

FileService.MoveLegacyUploads(app.Environment, app.Services.GetRequiredService<IOptions<UploadOptions>>().Value, app.Logger);
await DbSeeder.SeedAdminAsync(app.Services, app.Logger);

using (var scope = app.Services.CreateScope())
{
    var thumbnails = await scope.ServiceProvider.GetRequiredService<IFileService>().CreateMissingThumbnailsAsync();
    if (thumbnails > 0) app.Logger.LogInformation("Created {Count} missing photo thumbnails.", thumbnails);
}

if (args.Contains("--seed-demo"))
{
    if (!app.Environment.IsDevelopment())
    {
        // Demo accounts share a password published in the repository.
        throw new InvalidOperationException("--seed-demo is allowed only in the Development environment.");
    }
    await DemoSeeder.SeedAsync(app.Services, app.Logger);
    return;
}

app.Run();
