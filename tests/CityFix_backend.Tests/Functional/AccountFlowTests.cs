using System.Net;
using System.Text.Json;
using CityFix_backend.Models;
using CityFix_backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Tests.Functional;

[Trait("Category", "Functional")]
[Collection(nameof(WebCollection))]
public class AccountFlowTests(CityFixApp app)
{
    private static Dictionary<string, string> Registration(string email) => new()
    {
        ["FirstName"] = "Олена", ["LastName"] = "Коваль", ["Email"] = email, ["Phone"] = "+380501234567",
        ["Password"] = "Secret-123", ["ConfirmPassword"] = "Secret-123"
    };

    private static string PathOf(string link) => new Uri(link).PathAndQuery;

    [Fact]
    public async Task Visitor_registers_confirms_the_email_and_signs_in()
    {
        var email = TestData.UniqueEmail("visitor");
        var client = app.Client();

        Assert.Contains(Uk.Text("Auth.Register.Submit"), await client.PageAsync("/Account/Register"));
        var registered = await client.PostFormAsync("/Account/Register", Registration(email));
        Assert.Equal((HttpStatusCode.Redirect, "/Account/CheckEmail"), (registered.StatusCode, registered.Location()));
        Assert.Contains(email, await client.PageAsync("/Account/CheckEmail"));

        var tooEarly = await client.PostFormAsync("/Account/Login", new() { ["Email"] = email, ["Password"] = "Secret-123" });
        Assert.Contains(Uk.Text("Auth.EmailNotConfirmed"), await tooEarly.TextAsync());

        var letter = await app.Email.WaitForAsync(email);
        Assert.Contains("/Account/ConfirmEmail?token=", letter.Link);
        var confirmed = await client.GetAsync(PathOf(letter.Link));
        Assert.Equal("/Account/Login", confirmed.Location());
        Assert.Contains(Uk.Text("Auth.EmailConfirmed"), await client.PageAsync("/Account/Login"));

        var signedIn = await client.PostFormAsync("/Account/Login", new() { ["Email"] = email.ToUpperInvariant(), ["Password"] = "Secret-123" });
        Assert.Equal("/Dashboard", signedIn.Location());
        Assert.Contains("Олена", await client.PageAsync("/Dashboard"));
    }

    [Fact]
    public async Task Registration_form_errors_are_shown_and_nothing_is_saved()
    {
        var email = TestData.UniqueEmail("bad");
        var form = Registration(email);
        form["ConfirmPassword"] = "other";
        form["HasAssociation"] = "true";

        var response = await app.Client().PostFormAsync("/Account/Register", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.TextAsync();
        Assert.Contains(Uk.Text("Auth.CodeRequired"), page);
        Assert.False(await app.DbAsync((db, _) => db.Users.AnyAsync(u => u.Email == email)));
    }

    [Fact]
    public async Task Registering_with_a_taken_email_looks_the_same_but_mails_the_owner()
    {
        var owner = await app.DataAsync(d => d.UserAsync());
        var response = await app.Client().PostFormAsync("/Account/Register", Registration(owner.Email));

        Assert.Equal("/Account/CheckEmail", response.Location());
        Assert.Contains("/Account/ResetPassword?token=", (await app.Email.WaitForAsync(owner.Email)).Link);
        Assert.Equal(1, await app.DbAsync((db, _) => db.Users.CountAsync(u => u.Email == owner.Email)));
    }

    [Fact]
    public async Task Wrong_password_keeps_the_visitor_on_the_login_page()
    {
        var user = await app.DataAsync(d => d.UserAsync());
        var client = app.Client();
        var response = await client.PostFormAsync("/Account/Login", new() { ["Email"] = user.Email, ["Password"] = "wrong" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(Uk.Text("Auth.LoginFailed"), await response.TextAsync());
        Assert.Equal("/Account/Login", new Uri(new Uri("http://localhost"), (await client.GetAsync("/Dashboard")).Location()).AbsolutePath);
    }

    [Fact]
    public async Task Repeated_failures_lock_the_account()
    {
        var user = await app.DataAsync(d => d.UserAsync());
        var client = app.Client();
        for (var i = 0; i < 5; i++) await client.PostFormAsync("/Account/Login", new() { ["Email"] = user.Email, ["Password"] = "wrong" });

        var locked = await client.PostFormAsync("/Account/Login", new() { ["Email"] = user.Email, ["Password"] = TestData.Password });
        Assert.Contains(Uk.Format("Auth.LockedOut", 15), await locked.TextAsync());
    }

    [Fact]
    public async Task Auth_cookie_is_http_only_lax_and_persistent_only_when_asked()
    {
        var user = await app.DataAsync(d => d.UserAsync());

        var session = await app.Client().PostFormAsync("/Account/Login", new() { ["Email"] = user.Email, ["Password"] = TestData.Password });
        var cookie = session.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Cookies="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);

        var remembered = await app.Client().PostFormAsync("/Account/Login", new() { ["Email"] = user.Email, ["Password"] = TestData.Password, ["RememberMe"] = "true" });
        Assert.Contains("expires=", remembered.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Cookies=")), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/Appeals?tab=new", "/Appeals?tab=new")]
    [InlineData("https://evil.example/phish", "/Dashboard")]
    [InlineData("//evil.example", "/Dashboard")]
    public async Task Login_returns_only_to_local_pages(string returnUrl, string expected)
    {
        var user = await app.DataAsync(d => d.UserAsync());
        var response = await app.Client().PostFormAsync("/Account/Login",
            new() { ["Email"] = user.Email, ["Password"] = TestData.Password, ["ReturnUrl"] = returnUrl });
        Assert.Equal(expected, response.Location());
    }

    [Fact]
    public async Task Forgotten_password_is_reset_through_the_emailed_link_once()
    {
        var user = await app.DataAsync(d => d.UserAsync());
        var client = app.Client();

        var asked = await client.PostFormAsync("/Account/ForgotPassword", new() { ["Email"] = user.Email });
        Assert.Equal("/Account/Login", asked.Location());
        var link = PathOf((await app.Email.WaitForAsync(user.Email)).Link);
        var token = HttpExtensions.Token(await client.PageAsync(link));
        var reset = await client.PostAsync("/Account/ResetPassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Token"] = Uri.UnescapeDataString(link[(link.IndexOf("token=") + 6)..]), ["NewPassword"] = "Fresh-Passw0rd",
            ["ConfirmPassword"] = "Fresh-Passw0rd", ["__RequestVerificationToken"] = token
        }));

        Assert.Equal("/Account/Login", reset.Location());
        await app.SignInAsync(user.Email, "Fresh-Passw0rd");
        var old = await app.Client().PostFormAsync("/Account/Login", new() { ["Email"] = user.Email, ["Password"] = TestData.Password });
        Assert.Equal(HttpStatusCode.OK, old.StatusCode);
        Assert.Equal("/Account/ForgotPassword", (await client.GetAsync(link)).Location());
    }

    [Fact]
    public async Task Unknown_email_gets_the_same_answer_and_no_letter()
    {
        var email = TestData.UniqueEmail("ghost");
        var response = await app.Client().PostFormAsync("/Account/ForgotPassword", new() { ["Email"] = email });
        Assert.Equal("/Account/Login", response.Location());
        await Task.Delay(200);
        Assert.Empty(app.Email.To(email));
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var user = await app.DataAsync(d => d.UserAsync());
        var client = await app.SignInAsync(user.Email);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Dashboard")).StatusCode);

        var logout = await client.PostFormAsync("/Account/Logout", [], tokenFrom: "/Dashboard");

        Assert.Equal("/", logout.Location());
        Assert.StartsWith("http://localhost/Account/Login", (await client.GetAsync("/Dashboard")).Location());
    }

    [Fact]
    public async Task Changing_the_password_signs_out_other_browsers()
    {
        var user = await app.DataAsync(d => d.UserAsync());
        var laptop = await app.SignInAsync(user.Email);
        var phone = await app.SignInAsync(user.Email);

        var changed = await laptop.PostFormAsync("/Account/ChangePassword", new()
        {
            ["ChangePassword.CurrentPassword"] = TestData.Password, ["ChangePassword.NewPassword"] = "Another-Passw0rd", ["ChangePassword.ConfirmPassword"] = "Another-Passw0rd"
        }, tokenFrom: "/Account/Profile");

        Assert.Equal("/Account/Profile#security", changed.Location());
        Assert.Equal(HttpStatusCode.OK, (await laptop.GetAsync("/Dashboard")).StatusCode);
        Assert.StartsWith("http://localhost/Account/Login", (await phone.GetAsync("/Dashboard")).Location());
    }

    [Fact]
    public async Task Wrong_current_password_does_not_change_anything()
    {
        var user = await app.DataAsync(d => d.UserAsync());
        var client = await app.SignInAsync(user.Email);
        var response = await client.PostFormAsync("/Account/ChangePassword", new()
        {
            ["ChangePassword.CurrentPassword"] = "wrong", ["ChangePassword.NewPassword"] = "Another-Passw0rd", ["ChangePassword.ConfirmPassword"] = "Another-Passw0rd"
        }, tokenFrom: "/Account/Profile");
        Assert.Contains(Uk.Text("Profile.WrongPassword"), await response.TextAsync());
        await app.SignInAsync(user.Email);
    }

    [Fact]
    public async Task Blocking_a_user_ends_the_open_session()
    {
        var user = await app.DataAsync(d => d.UserAsync());
        var client = await app.SignInAsync(user.Email);
        await app.DbAsync((db, _) => db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsBlocked, true)));

        Assert.StartsWith("http://localhost/Account/Login", (await client.GetAsync("/Dashboard")).Location());
    }

    [Fact]
    public async Task Seeded_administrator_must_replace_the_temporary_password_first()
    {
        var client = await app.SignInAsync(CityFixApp.AdminEmail, CityFixApp.AdminPassword);

        Assert.Equal("/Account/Profile", (await client.GetAsync("/Users")).Location());
        Assert.Contains(Uk.Text("Auth.MustChangePassword"), await client.PageAsync("/Account/Profile"));

        await client.PostFormAsync("/Account/ChangePassword", new()
        {
            ["ChangePassword.CurrentPassword"] = CityFixApp.AdminPassword, ["ChangePassword.NewPassword"] = "Changed-Admin-1", ["ChangePassword.ConfirmPassword"] = "Changed-Admin-1"
        }, tokenFrom: "/Account/Profile");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Users")).StatusCode);
    }

    [Fact]
    public async Task Profile_email_change_is_confirmed_through_the_new_address()
    {
        var user = await app.DataAsync(d => d.UserAsync());
        var client = await app.SignInAsync(user.Email);
        var next = TestData.UniqueEmail("next");

        var saved = await client.PostFormAsync("/Account/UpdateProfile",
            new() { ["Profile.FirstName"] = "Тарас", ["Profile.LastName"] = "Шевченко", ["Profile.Email"] = next }, tokenFrom: "/Account/Profile");
        Assert.Equal("/Account/Profile#personal", saved.Location());
        Assert.Equal(user.Email, await app.DbAsync((db, _) => db.Users.Where(u => u.Id == user.Id).Select(u => u.Email).SingleAsync()));

        await client.GetAsync(PathOf((await app.Email.WaitForAsync(next)).Link));
        Assert.Equal(next, await app.DbAsync((db, _) => db.Users.Where(u => u.Id == user.Id).Select(u => u.Email).SingleAsync()));
    }

    [Fact]
    public async Task Residents_join_an_association_with_its_code()
    {
        var acab = await app.DataAsync(d => d.AcabAsync());
        var user = await app.DataAsync(d => d.UserAsync());
        var client = await app.SignInAsync(user.Email);

        var joined = await client.PostFormAsync("/Account/JoinAssociation", new() { ["code"] = acab.ResidentCode.ToLowerInvariant() }, tokenFrom: "/Account/Profile");

        Assert.Equal("/Account/Profile#association", joined.Location());
        Assert.Equal(acab.Id, await app.DbAsync((db, _) => db.Users.Where(u => u.Id == user.Id).Select(u => u.AcabId).SingleAsync()));
        Assert.True(await app.DbAsync((db, _) => db.AuditLog.AnyAsync(e => e.Action == "user.association" && e.ActorId == user.Id)));
    }

    [Fact]
    public async Task Personal_data_can_be_downloaded()
    {
        var user = await app.DataAsync(d => d.UserAsync());
        var client = await app.SignInAsync(user.Email);

        var response = await client.GetAsync("/Account/ExportData");

        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("cityfix-my-data-", response.Content.Headers.ContentDisposition!.FileNameStar);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(user.Email, json.RootElement.GetProperty("profile").GetProperty("email").GetString());
    }

    [Fact]
    public async Task Account_deletion_needs_the_password_and_anonymises_the_user()
    {
        var user = await app.DataAsync(d => d.UserAsync());
        var client = await app.SignInAsync(user.Email);

        var refused = await client.PostFormAsync("/Account/DeleteAccount", new() { ["password"] = "wrong" }, tokenFrom: "/Account/Profile");
        Assert.Equal("/Account/Profile#data", refused.Location());

        var deleted = await client.PostFormAsync("/Account/DeleteAccount", new() { ["password"] = TestData.Password }, tokenFrom: "/Account/Profile");
        Assert.Equal("/Account/Login", deleted.Location());
        var stored = await app.DbAsync((db, _) => db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id));
        Assert.Equal(($"deleted-{user.Id}@deleted.invalid", true), (stored.Email, stored.IsBlocked));
        Assert.StartsWith("http://localhost/Account/Login", (await client.GetAsync("/Dashboard")).Location());
    }
}
