using System.Globalization;
using CityFix_backend;
using CityFix_backend.Data;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

// The server locale (uk-UA) expects a decimal comma, but Lat/Lng are entered and posted with a dot.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// Resources/SharedResource.resx is Ukrainian (the default), SharedResource.en.resx is English.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
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
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

builder.Services.AddScoped<IAppealService, AppealService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await DbSeeder.SeedAdminAsync(app.Services, app.Logger);

app.Run();
