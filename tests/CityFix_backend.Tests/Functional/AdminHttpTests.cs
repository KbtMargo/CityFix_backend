using System.Net;
using System.Text.RegularExpressions;
using CityFix_backend.Models;
using CityFix_backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Tests.Functional;

[Trait("Category", "Functional")]
[Collection(nameof(WebCollection))]
public class AdminHttpTests(CityFixApp app)
{
    private async Task<HttpClient> AdminAsync() => await app.SignInAsync((await app.DataAsync(d => d.AdminAsync())).Email);

    [Fact]
    public async Task Administrator_manages_categories_and_every_change_is_audited()
    {
        var admin = await AdminAsync();
        var name = "Озеленення " + Guid.NewGuid().ToString("N")[..6];

        Assert.Equal("/Categories", (await admin.PostFormAsync("/Categories/Create", new() { ["Name"] = $"  {name}  ", ["NameEn"] = "Greenery" })).Location());
        var id = await app.DbAsync((db, _) => db.Categories.Where(c => c.Name == name).Select(c => c.Id).SingleAsync());
        Assert.Contains(name, await admin.PageAsync($"/Categories?q={Uri.EscapeDataString(name)}"));

        await admin.PostFormAsync($"/Categories/Edit/{id}", new() { ["Name"] = name + " 2", ["NameEn"] = "" });
        Assert.Null(await app.DbAsync((db, _) => db.Categories.Where(c => c.Id == id).Select(c => c.NameEn).SingleAsync()));

        await admin.PostFormAsync($"/Categories/Delete/{id}", []);
        Assert.False(await app.DbAsync((db, _) => db.Categories.AnyAsync(c => c.Id == id)));

        var audit = await admin.PageAsync($"/Audit?q={Uri.EscapeDataString(name)}");
        Assert.Contains("category.create", audit);
        Assert.Contains("category.update", audit);
        Assert.Contains("category.delete", audit);
    }

    [Fact]
    public async Task Category_with_appeals_cannot_be_deleted()
    {
        var admin = await AdminAsync();
        var n = await app.DataAsync(d => d.NeighbourhoodAsync());
        await app.DataAsync(d => d.AppealAsync(n.Resident, n.Category));

        var response = await admin.PostFormAsync($"/Categories/Delete/{n.Category.Id}", []);

        Assert.Equal($"/Categories/Delete/{n.Category.Id}", response.Location());
        Assert.True(await app.DbAsync((db, _) => db.Categories.AnyAsync(c => c.Id == n.Category.Id)));
    }

    [Fact]
    public async Task Empty_category_name_is_rejected()
    {
        var admin = await AdminAsync();
        var response = await admin.PostFormAsync("/Categories/Create", new() { ["Name"] = "" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(Uk.Text("Validation.Required").Split('{')[0], await response.TextAsync());
    }

    [Fact]
    public async Task Recipients_are_managed_like_categories()
    {
        var admin = await AdminAsync();
        var name = "Теплокомуненерго " + Guid.NewGuid().ToString("N")[..6];
        await admin.PostFormAsync("/Recipients/Create", new() { ["Name"] = name, ["ContactInfo"] = " 0382 65 00 00 " });
        Assert.Equal("0382 65 00 00", await app.DbAsync((db, _) => db.Recipients.Where(r => r.Name == name).Select(r => r.ContactInfo).SingleAsync()));
    }

    [Fact]
    public async Task New_association_hands_out_codes_that_let_people_register()
    {
        var admin = await AdminAsync();
        var name = "ОСББ Сонячне " + Guid.NewGuid().ToString("N")[..6];

        var created = await admin.PostFormAsync("/Acabs/Create", new() { ["Name"] = name, ["Address"] = "вул. Сонячна, 5" });
        var editPage = await admin.PageAsync(created.Location()!);
        var acab = await app.DbAsync((db, _) => db.Acabs.AsNoTracking().SingleAsync(a => a.Name == name));
        Assert.Contains(acab.ResidentCode, editPage);
        Assert.Contains(acab.HeadCode, editPage);

        var email = TestData.UniqueEmail("head");
        await app.Client().PostFormAsync("/Account/Register", new()
        {
            ["FirstName"] = "Ірина", ["LastName"] = "Мельник", ["Email"] = email, ["Password"] = "Secret-123", ["ConfirmPassword"] = "Secret-123",
            ["HasAssociation"] = "true", ["InvitationCode"] = acab.HeadCode
        });
        var head = await app.DbAsync((db, _) => db.Users.AsNoTracking().SingleAsync(u => u.Email == email));
        Assert.Equal((UserRole.AcabAdmin, acab.Id), (head.Role, head.AcabId));

        await admin.PostFormAsync($"/Acabs/RegenerateCode/{acab.Id}", new() { ["kind"] = "Resident" }, tokenFrom: $"/Acabs/Edit/{acab.Id}");
        Assert.NotEqual(acab.ResidentCode, await app.DbAsync((db, _) => db.Acabs.Where(a => a.Id == acab.Id).Select(a => a.ResidentCode).SingleAsync()));
        Assert.Equal($"/Acabs/Delete/{acab.Id}", (await admin.PostFormAsync($"/Acabs/Delete/{acab.Id}", [])).Location());
    }

    [Fact]
    public async Task Created_user_signs_in_with_the_shown_password_and_must_change_it()
    {
        var admin = await AdminAsync();
        var email = TestData.UniqueEmail("staff");

        var created = await admin.PostFormAsync("/Users/Create", new() { ["FirstName"] = "Марія", ["Email"] = email, ["Role"] = "Resident" });
        var flash = await admin.PageAsync(created.Location()!);
        var password = Regex.Match(flash, @"пароль: (\S+)\.").Groups[1].Value;
        Assert.Equal(12, password.Length);

        var user = await app.SignInAsync(email, password);
        Assert.Equal("/Account/Profile", (await user.GetAsync("/Appeals")).Location());
        Assert.True(await app.DbAsync((db, _) => db.AuditLog.AnyAsync(e => e.Action == "user.create" && e.Target.Contains(email))));
    }

    [Fact]
    public async Task Administrator_blocks_resets_and_deletes_users()
    {
        var admin = await AdminAsync();
        var user = await app.DataAsync(d => d.UserAsync());
        var session = await app.SignInAsync(user.Email);

        await admin.PostFormAsync($"/Users/SetBlocked/{user.Id}", new() { ["blocked"] = "true" }, tokenFrom: $"/Users/Edit/{user.Id}");
        Assert.StartsWith("http://localhost/Account/Login", (await session.GetAsync("/Dashboard")).Location());

        await admin.PostFormAsync($"/Users/ResetPassword/{user.Id}", [], tokenFrom: $"/Users/Edit/{user.Id}");
        Assert.True(await app.DbAsync((db, _) => db.Users.Where(u => u.Id == user.Id).Select(u => u.MustChangePassword).SingleAsync()));

        Assert.Equal("/Users", (await admin.PostFormAsync($"/Users/Delete/{user.Id}", [])).Location());
        Assert.False(await app.DbAsync((db, _) => db.Users.AnyAsync(u => u.Id == user.Id)));
    }

    [Fact]
    public async Task Head_sees_residents_of_the_association_but_cannot_edit_them()
    {
        var n = await app.DataAsync(d => d.NeighbourhoodAsync());
        var head = await app.SignInAsync(n.Head.Email);

        var list = await head.PageAsync("/Users");
        Assert.Contains(n.Resident.Email, list);
        Assert.Contains("Олена", await head.PageAsync($"/Users/Details/{n.Resident.Id}"));

        var stranger = await app.DataAsync(d => d.UserAsync());
        Assert.StartsWith("http://localhost/Account/AccessDenied", (await head.GetAsync($"/Users/Details/{stranger.Id}")).Location());
        Assert.StartsWith("http://localhost/Account/AccessDenied", (await head.GetAsync($"/Users/Edit/{n.Resident.Id}")).Location());
    }

    [Theory]
    [InlineData("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("pdf", "application/pdf")]
    public async Task Analytics_reports_download(string format, string contentType)
    {
        var admin = await AdminAsync();
        var response = await admin.GetAsync($"/Analytics/Export?format={format}");
        Assert.Equal(contentType, response.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("cityfix-analytics-", response.Content.Headers.ContentDisposition!.FileNameStar);
    }
}

[Trait("Category", "Functional")]
public class StartupConfigurationTests
{
    private static async Task<Exception?> StartAsync(string environment, Dictionary<string, string?> settings)
    {
        var app = new CityFixApp { Environment = environment };
        foreach (var (k, v) in settings) app.Settings[k] = v;
        try
        {
            await app.InitializeAsync();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
        finally
        {
            await app.Database.DisposeAsync();
        }
    }

    [Fact]
    public async Task Production_refuses_to_start_without_a_public_url()
    {
        var error = await StartAsync("Production", new() { ["Email:SmtpHost"] = "smtp.example", ["Notifications:PublicUrl"] = "" });
        Assert.Contains("Notifications:PublicUrl", Assert.IsType<InvalidOperationException>(error).Message);
    }

    [Fact]
    public async Task Production_refuses_to_start_without_smtp()
    {
        var error = await StartAsync("Production", new() { ["Notifications:PublicUrl"] = "https://cityfix.example" });
        Assert.Contains("Email:SmtpHost", Assert.IsType<InvalidOperationException>(error).Message);
    }

    [Fact]
    public async Task Production_with_full_settings_starts_and_hardens_the_responses()
    {
        var app = new CityFixApp { Environment = "Production" };
        app.Settings["Notifications:PublicUrl"] = "https://cityfix.example";
        app.Settings["Email:SmtpHost"] = "smtp.example";
        await app.InitializeAsync();
        try
        {
            var https = app.Client(https: true);
            https.BaseAddress = new Uri("https://cityfix.example"); // HSTS is never sent to localhost
            var response = await https.GetAsync("/");
            var csp = string.Join(";", response.Headers.GetValues("Content-Security-Policy"));
            Assert.DoesNotContain("localhost", csp);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("max-age=", response.Headers.GetValues("Strict-Transport-Security").Single());
            var cookie = (await https.GetAsync("/Account/Login")).Headers.GetValues("Set-Cookie").First();
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }
}
