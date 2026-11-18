using System.Net;
using CityFix_backend.Models;
using CityFix_backend.Tests.Infrastructure;

namespace CityFix_backend.Tests.Functional;

[Trait("Category", "Functional")]
[Collection(nameof(WebCollection))]
public class SiteSecurityTests(CityFixApp app)
{
    [Fact]
    public async Task Health_check_reports_a_reachable_database()
    {
        var response = await app.Client().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Landing_page_is_public_and_sends_security_headers()
    {
        var response = await app.Client().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csp = string.Join(";", response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Theory]
    [InlineData("/Dashboard")]
    [InlineData("/Appeals")]
    [InlineData("/Appeals/Create")]
    [InlineData("/Analytics")]
    [InlineData("/Notifications")]
    [InlineData("/Account/Profile")]
    [InlineData("/Users")]
    [InlineData("/Audit")]
    [InlineData("/Geo/Search?q=abc")]
    [InlineData("/Appeals/Photo/1")]
    [InlineData("/Account/ExportData")]
    public async Task Guests_are_sent_to_the_login_page(string url)
    {
        var response = await app.Client().GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("http://localhost/Account/Login?ReturnUrl=", response.Location());
    }

    [Fact]
    public async Task Unknown_pages_get_a_friendly_404()
    {
        var response = await app.Client().GetAsync("/No/Such/Page");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("<html", await response.Content.ReadAsStringAsync());
    }

    public static TheoryData<string, UserRole, bool> Access => new()
    {
        { "/Users", UserRole.Resident, false },
        { "/Users", UserRole.AcabAdmin, true },
        { "/Users", UserRole.Administrator, true },
        { "/Users/Create", UserRole.AcabAdmin, false },
        { "/Acabs", UserRole.AcabAdmin, false },
        { "/Acabs", UserRole.Administrator, true },
        { "/Categories", UserRole.Resident, false },
        { "/Categories", UserRole.Administrator, true },
        { "/Recipients", UserRole.AcabAdmin, false },
        { "/Recipients", UserRole.Administrator, true },
        { "/Audit", UserRole.AcabAdmin, false },
        { "/Audit", UserRole.Administrator, true },
        { "/Appeals/Export?format=csv", UserRole.Resident, false },
        { "/Appeals/Export?format=csv", UserRole.AcabAdmin, true },
        { "/Dashboard", UserRole.Resident, true },
        { "/Analytics", UserRole.Resident, true },
        { "/Analytics", UserRole.AcabAdmin, true }
    };

    [Theory]
    [MemberData(nameof(Access))]
    public async Task Pages_are_open_only_to_the_right_roles(string url, UserRole role, bool allowed)
    {
        var user = await app.DataAsync(async d => role == UserRole.AcabAdmin ? await d.HeadAsync(await d.AcabAsync()) : await d.UserAsync(role));
        var client = await app.SignInAsync(user.Email);

        var response = await client.GetAsync(url);

        if (allowed)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        else
        {
            Assert.StartsWith("http://localhost/Account/AccessDenied", response.Location());
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(response.Location())).StatusCode);
        }
    }

    [Fact]
    public async Task Navigation_shows_admin_sections_only_to_administrators()
    {
        var admin = await app.DataAsync(d => d.AdminAsync());
        var resident = await app.DataAsync(d => d.UserAsync());

        var adminPage = await (await app.SignInAsync(admin.Email)).PageAsync("/Dashboard");
        var residentPage = await (await app.SignInAsync(resident.Email)).PageAsync("/Dashboard");

        Assert.Contains("href=\"/Audit\"", adminPage);
        Assert.Contains("href=\"/Users\"", adminPage);
        Assert.DoesNotContain("href=\"/Audit\"", residentPage);
        Assert.DoesNotContain("href=\"/Users\"", residentPage);
    }

    [Fact]
    public async Task Forms_without_an_antiforgery_token_are_refused()
    {
        var user = await app.DataAsync(d => d.UserAsync());
        var client = await app.SignInAsync(user.Email);

        var login = await app.Client().PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = user.Email, ["Password"] = TestData.Password }));
        var delete = await client.PostAsync("/Account/DeleteAccount", new FormUrlEncodedContent(new Dictionary<string, string> { ["password"] = TestData.Password }));

        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        Assert.False((await app.DbAsync((db, _) => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Users, u => u.Id == user.Id))).IsBlocked);
    }

    [Fact]
    public async Task Token_from_another_session_is_refused()
    {
        var first = await app.SignInAsync((await app.DataAsync(d => d.UserAsync())).Email);
        var second = await app.SignInAsync((await app.DataAsync(d => d.UserAsync())).Email);
        var foreignToken = HttpExtensions.Token(await first.PageAsync("/Account/Profile"));

        var response = await second.PostAsync("/Account/LeaveAssociation", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = foreignToken }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Too_many_sign_in_attempts_from_one_address_are_throttled()
    {
        var client = app.Client("192.0.2.77");
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 12; i++)
        {
            statuses.Add((await client.PostFormAsync("/Account/Login", new() { ["Email"] = "nobody@test.local", ["Password"] = "x" })).StatusCode);
        }

        Assert.Equal(10, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.Equal(HttpStatusCode.OK, (await app.Client("192.0.2.78").PostFormAsync("/Account/Login", new() { ["Email"] = "nobody@test.local", ["Password"] = "x" })).StatusCode);
    }

    [Fact]
    public async Task Language_switch_sets_a_cookie_and_translates_the_site()
    {
        var client = app.Client();

        var response = await client.PostFormAsync("/Culture/Set", new() { ["culture"] = "en", ["returnUrl"] = "/Account/Login" }, tokenFrom: "/Account/Login");

        Assert.Equal("/Account/Login", response.Location());
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Culture=") && c.Contains("uic%3Den"));
        string english;
        using (new UiCulture("en")) english = CityFix_backend.Helpers.SharedText.Get("Auth.LoginFailed");
        var page = await client.PostFormAsync("/Account/Login", new() { ["Email"] = "nobody@test.local", ["Password"] = "x" });
        Assert.Contains(english, await page.TextAsync());
        Assert.Contains("<html lang=\"en\"", await client.PageAsync("/Account/Login"));
    }

    [Fact]
    public async Task Language_switch_never_redirects_to_another_site()
    {
        var response = await app.Client().PostFormAsync("/Culture/Set", new() { ["culture"] = "en", ["returnUrl"] = "https://evil.example" }, tokenFrom: "/Account/Login");
        Assert.Equal("/", response.Location());
    }

    [Fact]
    public async Task Signed_in_users_switching_language_have_it_remembered()
    {
        var user = await app.DataAsync(d => d.UserAsync());
        var client = await app.SignInAsync(user.Email);
        await client.PostFormAsync("/Culture/Set", new() { ["culture"] = "en", ["returnUrl"] = "/Dashboard" }, tokenFrom: "/Dashboard");
        Assert.Equal("en", await app.DbAsync((db, _) => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Users.Where(u => u.Id == user.Id).Select(u => u.Language))));
    }

    [Theory]
    [InlineData("/css/site.css", "text/css")]
    [InlineData("/js/map-picker.js", "text/javascript")]
    [InlineData("/favicon.ico", "image/x-icon")]
    public async Task Static_files_are_served(string url, string contentType)
    {
        var response = await app.Client().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Uploaded_photos_are_not_reachable_as_static_files()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client().GetAsync("/uploads/appeals/1/photo.jpg")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client().GetAsync("/App_Data/keys/key.xml")).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Resident)]
    [InlineData(UserRole.AcabAdmin)]
    [InlineData(UserRole.Administrator)]
    public async Task Every_main_page_renders_for_each_role(UserRole role)
    {
        var user = await app.DataAsync(async d => role == UserRole.AcabAdmin ? await d.HeadAsync(await d.AcabAsync()) : await d.UserAsync(role));
        var client = await app.SignInAsync(user.Email);
        foreach (var url in new[] { "/Dashboard", "/Appeals", "/Appeals/Create", "/Analytics", "/Notifications", "/Notifications?tab=all", "/Account/Profile", "/Dashboard?tab=closed&page=99&q=%25" })
        {
            await client.PageAsync(url);
        }
    }
}
