using System.Globalization;
using System.Threading.RateLimiting;
using CityFix_backend;
using CityFix_backend.Controllers;
using CityFix_backend.Data;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

// The server locale (uk-UA) expects a decimal comma, but Lat/Lng are entered and posted with a dot.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// Resources/SharedResource.resx is Ukrainian (the default), SharedResource.en.resx is English.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews(options => options.Filters.Add<ServiceExceptionFilter>())
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));

// Model-binding errors (e.g. nothing chosen in a required dropdown) in the user's language.
builder.Services.AddOptions<MvcOptions>().Configure<IStringLocalizerFactory>((options, factory) =>
{
    var localizer = factory.Create(typeof(SharedResource));
    options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => localizer["Validation.Choose"]);
    options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((value, field) => localizer["Validation.Invalid", value, field]);
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), o => o.UseNetTopologySuite()));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Blocked or deleted accounts are signed out, and role changes apply, at the next request.
        options.Events.OnValidatePrincipal = CookieValidator.ValidateAsync;
    });

// Sign-in and registration (and with it invitation-code guessing): 10 attempts per minute per address.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection(UploadOptions.Section));
builder.Services.Configure<AppealOptions>(builder.Configuration.GetSection(AppealOptions.Section));
builder.Services.Configure<GeocodingOptions>(builder.Configuration.GetSection(GeocodingOptions.Section));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.Section));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Section));
builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection(SmsOptions.Section));
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.Section));

builder.Services.Configure<TranslationOptions>(builder.Configuration.GetSection(TranslationOptions.Section));

// PDF reports (QuestPDF): the free Community licence covers this project.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
// Cyrillic needs real fonts: use the system ones (Segoe UI / Arial on Windows, DejaVu Sans on Linux).
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
builder.Services.AddHttpClient<IGeocodingService, NominatimGeocodingService>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<GeocodingOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
});

// Email: SMTP when Email:SmtpHost is set, otherwise letters are written to the log (development).
// SMS: Twilio when Sms:Provider is "Twilio", otherwise the log. Copies of notifications are sent in the background.
if (string.IsNullOrWhiteSpace(builder.Configuration[$"{EmailOptions.Section}:{nameof(EmailOptions.SmtpHost)}"]))
{
    builder.Services.AddSingleton<IEmailSender, LogEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}
if (string.Equals(builder.Configuration[$"{SmsOptions.Section}:{nameof(SmsOptions.Provider)}"], "Twilio", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<ISmsSender, TwilioSmsSender>(client => client.Timeout = TimeSpan.FromSeconds(10));
}
else
{
    builder.Services.AddSingleton<ISmsSender, LogSmsSender>();
}
builder.Services.AddHostedService<NotificationDispatcher>();

// Machine translation: DeepL with Translation:DeepLApiKey, otherwise the free MyMemory API. Runs in the background.
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// 404 and friends render the design's ErrorPage instead of an empty response.
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");

app.UseHttpsRedirection();

// MapStaticAssets only knows files that existed at build time; uploaded photos are added at runtime.
app.UseStaticFiles();

// Only the UI language is switchable (uk / en), and only through the culture cookie that
// CultureController writes. The formatting culture always stays invariant, so a request in
// Ukrainian never turns Lat/Lng parsing into uk-UA (decimal comma).
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
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await DbSeeder.SeedAdminAsync(app.Services, app.Logger);

// dotnet run -- --seed-demo : fills the database with demo data (Data/DemoSeeder.cs) and exits.
if (args.Contains("--seed-demo"))
{
    await DemoSeeder.SeedAsync(app.Services, app.Logger);
    return;
}

app.Run();
