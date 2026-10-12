using System.Globalization;
using CityFix_backend;
using CityFix_backend.Data;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Only the UI language is switchable (uk / en). The culture used for parsing and formatting
// numbers is pinned to whatever the app already ran with, so decimal handling is unchanged.
var formattingCulture = CultureInfo.DefaultThreadCurrentCulture ?? CultureInfo.CurrentCulture;
var uiCultures = new[] { new CultureInfo("uk"), new CultureInfo("en") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(formattingCulture, uiCultures[0]),
    SupportedCultures = new[] { formattingCulture },
    SupportedUICultures = uiCultures,
});

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
