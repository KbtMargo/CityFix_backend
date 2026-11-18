using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CityFix_backend.Models;
using CityFix_backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Tests.Functional;

[Trait("Category", "Functional")]
[Collection(nameof(WebCollection))]
public class AppealHttpTests(CityFixApp app)
{
    private Task<Neighbourhood> NeighbourhoodAsync() => app.DataAsync(d => d.NeighbourhoodAsync());

    private static async Task<HttpResponseMessage> CreateAppealAsync(HttpClient client, Neighbourhood n, string title, params byte[][] photos)
    {
        var token = HttpExtensions.Token(await client.PageAsync("/Appeals/Create"));
        using var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(title), "Title" },
            { new StringContent("Глибока яма біля під'їзду"), "Description" },
            { new StringContent("49.42161"), "Lat" },
            { new StringContent("26.99653"), "Lng" },
            { new StringContent("вул. Тестова, 1"), "Address" },
            { new StringContent(n.Category.Id.ToString()), "CategoryId" },
            { new StringContent("acab"), "Responsible" }
        };
        foreach (var photo in photos)
        {
            var content = new ByteArrayContent(photo);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(content, "photos", "photo.jpg");
        }
        return await client.PostAsync("/Appeals/Create", form);
    }

    private static int AppealIdFrom(HttpResponseMessage response) =>
        int.Parse(response.Location()!.Split("appealId=")[1]);

    [Fact]
    public async Task Resident_files_an_appeal_with_a_photo_and_the_head_gets_an_email()
    {
        var n = await NeighbourhoodAsync();
        var client = await app.SignInAsync(n.Resident.Email);

        var response = await CreateAppealAsync(client, n, "Яма біля школи №5", TestFiles.JpegWithSecrets());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var id = AppealIdFrom(response);
        var page = await client.PageAsync(response.Location()!);
        Assert.Contains("Яма біля школи №5", page);
        Assert.Contains(Uk.Text("Appeal.Created"), page);

        var stored = await app.DbAsync((db, _) => db.Appeals.Include(a => a.Files).SingleAsync(a => a.Id == id));
        Assert.Equal((n.Resident.Id, n.Acab.Id, 1), (stored.AuthorId, stored.AcabId, stored.Files.Count));
        var letter = await app.Email.WaitForAsync(n.Head.Email);
        Assert.Contains("Яма біля школи №5", letter.Message.Text);
    }

    [Fact]
    public async Task Invalid_appeal_form_is_shown_again_with_errors()
    {
        var n = await NeighbourhoodAsync();
        var client = await app.SignInAsync(n.Resident.Email);
        var token = HttpExtensions.Token(await client.PageAsync("/Appeals/Create"));

        var response = await client.PostAsync("/Appeals/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["Title"] = "Яма", ["Lat"] = "", ["Lng"] = "", ["Responsible"] = "acab"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("cf-input--error", await response.Content.ReadAsStringAsync());
        Assert.False(await app.DbAsync((db, _) => db.Appeals.AnyAsync(a => a.AuthorId == n.Resident.Id)));
    }

    [Fact]
    public async Task Head_moves_the_appeal_forward_and_the_author_is_told()
    {
        var n = await NeighbourhoodAsync();
        var appeal = await app.DataAsync(d => d.AppealAsync(n.Resident, n.Category, title: "Не працює ліфт"));
        var head = await app.SignInAsync(n.Head.Email);

        var response = await head.PostFormAsync("/Appeals/Process", new()
        {
            ["appealId"] = appeal.Id.ToString(), ["expectedStatus"] = "New", ["status"] = "InProgress", ["priority"] = "Urgent", ["comment"] = "Майстер прийде завтра"
        }, tokenFrom: "/Appeals");

        Assert.Equal($"/Appeals?appealId={appeal.Id}", response.Location());
        var stored = await app.DbAsync((db, _) => db.Appeals.AsNoTracking().SingleAsync(a => a.Id == appeal.Id));
        Assert.Equal((AppealStatus.InProgress, AppealPriority.Urgent), (stored.Status, stored.Priority));
        var letter = await app.Email.WaitForAsync(n.Resident.Email, m => m.Message.Text.Contains("Майстер прийде завтра"));
        Assert.Contains($"/Appeals?appealId={appeal.Id}", letter.Message.Html);
    }

    [Fact]
    public async Task Invalid_status_change_is_reported_and_the_form_values_kept()
    {
        var n = await NeighbourhoodAsync();
        var appeal = await app.DataAsync(d => d.AppealAsync(n.Resident, n.Category));
        var head = await app.SignInAsync(n.Head.Email);

        var response = await head.PostFormAsync("/Appeals/Process", new()
        {
            ["appealId"] = appeal.Id.ToString(), ["expectedStatus"] = "New", ["status"] = "Done", ["priority"] = "Normal", ["comment"] = "Готово"
        }, tokenFrom: "/Appeals");

        var page = await head.PageAsync(response.Location()!);
        Assert.Contains(Uk.Format("Validation.TransitionNotAllowed", AppealStatusName(AppealStatus.New), AppealStatusName(AppealStatus.Done)), page);
        Assert.Equal(AppealStatus.New, await app.DbAsync((db, _) => db.Appeals.Where(a => a.Id == appeal.Id).Select(a => a.Status).SingleAsync()));
    }

    private static string AppealStatusName(AppealStatus status)
    {
        using var _ = new UiCulture("uk");
        return CityFix_backend.Helpers.EnumExtensions.GetDisplayName(status);
    }

    [Fact]
    public async Task Residents_cannot_process_or_edit_other_peoples_appeals()
    {
        var n = await NeighbourhoodAsync();
        var appeal = await app.DataAsync(d => d.AppealAsync(n.Resident, n.Category));
        var neighbour = await app.SignInAsync(n.Neighbour.Email);

        var process = await neighbour.PostFormAsync("/Appeals/Process", new()
        {
            ["appealId"] = appeal.Id.ToString(), ["expectedStatus"] = "New", ["status"] = "Rejected", ["priority"] = "Normal", ["comment"] = "Ха"
        }, tokenFrom: "/Appeals");
        var edit = await neighbour.GetAsync($"/Appeals/Edit/{appeal.Id}");
        var delete = await neighbour.PostFormAsync($"/Appeals/Delete/{appeal.Id}", [], tokenFrom: "/Appeals");

        Assert.All(new[] { process, edit, delete }, r => Assert.StartsWith("http://localhost/Account/AccessDenied", r.Location()));
        Assert.Equal(AppealStatus.New, await app.DbAsync((db, _) => db.Appeals.Where(a => a.Id == appeal.Id).Select(a => a.Status).SingleAsync()));
    }

    [Fact]
    public async Task Missing_appeals_give_404()
    {
        var n = await NeighbourhoodAsync();
        var client = await app.SignInAsync(n.Resident.Email);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Appeals/Edit/987654")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Appeals/Photo/987654")).StatusCode);
    }

    [Fact]
    public async Task Author_edits_and_deletes_a_new_appeal()
    {
        var n = await NeighbourhoodAsync();
        var appeal = await app.DataAsync(d => d.AppealAsync(n.Resident, n.Category));
        var client = await app.SignInAsync(n.Resident.Email);

        var edited = await client.PostFormAsync($"/Appeals/Edit/{appeal.Id}", new()
        {
            ["Id"] = appeal.Id.ToString(), ["Title"] = "Уточнена назва ями", ["Lat"] = "49.42161", ["Lng"] = "26.99653",
            ["Address"] = "вул. Тестова, 1", ["CategoryId"] = n.Category.Id.ToString(), ["Responsible"] = n.Recipient.Id.ToString()
        });
        Assert.Equal($"/Appeals?appealId={appeal.Id}", edited.Location());
        var stored = await app.DbAsync((db, _) => db.Appeals.AsNoTracking().SingleAsync(a => a.Id == appeal.Id));
        Assert.Equal(("Уточнена назва ями", n.Recipient.Id), (stored.Title, stored.RecipientId));

        // Posting someone else's id in the form (the form value wins over the URL) must still be refused.
        var foreign = await app.DataAsync(d => d.AppealAsync(n.Neighbour, n.Category, title: "Чуже звернення"));
        var tampered = await client.PostFormAsync($"/Appeals/Edit/{appeal.Id}", new()
        {
            ["Id"] = foreign.Id.ToString(), ["Title"] = "Підмінена назва", ["Lat"] = "49.4", ["Lng"] = "26.9",
            ["CategoryId"] = n.Category.Id.ToString(), ["Responsible"] = "acab"
        }, tokenFrom: $"/Appeals/Edit/{appeal.Id}");
        Assert.StartsWith("http://localhost/Account/AccessDenied", tampered.Location());
        Assert.Equal("Чуже звернення", await app.DbAsync((db, _) => db.Appeals.Where(a => a.Id == foreign.Id).Select(a => a.Title).SingleAsync()));

        await client.PageAsync($"/Appeals/Delete/{appeal.Id}");
        Assert.Equal("/Appeals", (await client.PostFormAsync($"/Appeals/Delete/{appeal.Id}", [])).Location());
        Assert.False(await app.DbAsync((db, _) => db.Appeals.AnyAsync(a => a.Id == appeal.Id)));
    }

    [Fact]
    public async Task Head_deleting_a_residents_appeal_is_audited()
    {
        var n = await NeighbourhoodAsync();
        var appeal = await app.DataAsync(d => d.AppealAsync(n.Resident, n.Category));
        var head = await app.SignInAsync(n.Head.Email);

        await head.PostFormAsync($"/Appeals/Delete/{appeal.Id}", []);

        Assert.True(await app.DbAsync((db, _) => db.AuditLog.AnyAsync(e => e.Action == "appeal.delete" && e.Target.StartsWith($"Appeal #{appeal.Id} "))));
    }

    [Fact]
    public async Task Photos_are_served_privately_without_metadata()
    {
        var n = await NeighbourhoodAsync();
        var client = await app.SignInAsync(n.Resident.Email);
        var id = AppealIdFrom(await CreateAppealAsync(client, n, "Зламана лавка у дворі", TestFiles.JpegWithSecrets()));
        var fileId = await app.DbAsync((db, _) => db.AppealFiles.Where(f => f.AppealId == id).Select(f => f.Id).SingleAsync());

        var photo = await client.GetAsync($"/Appeals/Photo/{fileId}");
        var thumb = await client.GetAsync($"/Appeals/Photo/{fileId}?thumb=true");

        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType!.MediaType);
        Assert.Equal((true, TimeSpan.FromDays(1)), (photo.Headers.CacheControl!.Private, photo.Headers.CacheControl.MaxAge));
        Assert.Equal("nosniff", photo.Headers.GetValues("X-Content-Type-Options").Single());
        var bytes = await photo.Content.ReadAsByteArrayAsync();
        Assert.False(TestFiles.Contains(bytes, "SECRETCAM"));
        Assert.Equal("image/jpeg", thumb.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.OK, (await (await app.SignInAsync(n.Neighbour.Email)).GetAsync($"/Appeals/Photo/{fileId}")).StatusCode);
    }

    [Fact]
    public async Task Similar_appeals_endpoint_warns_about_duplicates_nearby()
    {
        var n = await NeighbourhoodAsync();
        var existing = await app.DataAsync(d => d.AppealAsync(n.Neighbour, n.Category, title: "Сусідська яма"));
        var client = await app.SignInAsync(n.Resident.Email);

        var response = await client.GetAsync($"/Appeals/Similar?lat={TestData.Lat}&lng={TestData.Lng}&categoryId={n.Category.Id}");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = json.RootElement.EnumerateArray().Single(e => e.GetProperty("id").GetInt32() == existing.Id);
        Assert.Equal("Сусідська яма", item.GetProperty("title").GetString());
        Assert.True(item.GetProperty("canSupport").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/Appeals/Similar?lat=91&lng=0&categoryId={n.Category.Id}")).StatusCode);
    }

    [Fact]
    public async Task Support_button_toggles_through_json()
    {
        var n = await NeighbourhoodAsync();
        var appeal = await app.DataAsync(d => d.AppealAsync(n.Resident, n.Category));
        var neighbour = await app.SignInAsync(n.Neighbour.Email);

        var on = await neighbour.PostFormAsync($"/Appeals/Support/{appeal.Id}", [], tokenFrom: "/Dashboard");
        using (var json = JsonDocument.Parse(await on.Content.ReadAsStringAsync()))
        {
            Assert.True(json.RootElement.GetProperty("supported").GetBoolean());
            Assert.Equal(1, json.RootElement.GetProperty("count").GetInt32());
        }

        var author = await app.SignInAsync(n.Resident.Email);
        var own = await author.PostFormAsync($"/Appeals/Support/{appeal.Id}", [], tokenFrom: "/Dashboard");
        Assert.Equal(HttpStatusCode.BadRequest, own.StatusCode);
        Assert.Contains("error", await own.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Address_lookup_uses_the_geocoder()
    {
        var client = await app.SignInAsync((await app.DataAsync(d => d.UserAsync())).Email);

        using (var reverse = JsonDocument.Parse(await (await client.GetAsync("/Geo/Reverse?lat=49.42&lng=26.98")).Content.ReadAsStringAsync()))
        {
            Assert.Equal(app.Geocoding.Address, reverse.RootElement.GetProperty("address").GetString());
        }
        Assert.Contains("\"lat\":49.42", await (await client.GetAsync("/Geo/Search?q=Майдан")).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/Geo/Reverse?lat=100&lng=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/Geo/Search?q=")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/Geo/Search?q=ab")).StatusCode);
    }

    [Theory]
    [InlineData("csv", "text/csv")]
    [InlineData("json", "application/json")]
    [InlineData("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("pdf", "application/pdf")]
    public async Task Staff_export_appeals_in_every_format(string format, string contentType)
    {
        var n = await NeighbourhoodAsync();
        await app.DataAsync(d => d.AppealAsync(n.Resident, n.Category, title: "Експортна яма"));
        var head = await app.SignInAsync(n.Head.Email);

        var response = await head.GetAsync($"/Appeals/Export?format={format}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType!.MediaType);
        Assert.Matches($@"^cityfix-appeals-\d{{8}}-\d{{4}}\.{format}$", response.Content.Headers.ContentDisposition!.FileNameStar);
        Assert.True((await response.Content.ReadAsByteArrayAsync()).Length > 100);
    }

    [Fact]
    public async Task Csv_export_is_excel_friendly_and_formula_safe()
    {
        var n = await NeighbourhoodAsync();
        await app.DataAsync(d => d.AppealAsync(n.Resident, n.Category, title: "=HYPERLINK(\"http://evil\",\"x\")"));
        await app.DataAsync(d => d.AppealAsync(n.Neighbour, n.Category, title: "Лавка, біля \"школи\""));
        var head = await app.SignInAsync(n.Head.Email);

        var bytes = await (await head.GetAsync($"/Appeals/Export?format=csv&categoryId={n.Category.Id}")).Content.ReadAsByteArrayAsync();

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var csv = Encoding.UTF8.GetString(bytes[3..]);
        Assert.Equal(3, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("\"'=HYPERLINK(\"\"http://evil\"\",\"\"x\"\")\"", csv);
        Assert.Contains("\"Лавка, біля \"\"школи\"\"\"", csv);
    }

    [Fact]
    public async Task Head_export_contains_only_the_association()
    {
        var n = await NeighbourhoodAsync();
        var outsider = await app.DataAsync(d => d.UserAsync());
        await app.DataAsync(d => d.AppealAsync(n.Resident, n.Category, title: "Своя яма"));
        await app.DataAsync(d => d.AppealAsync(outsider, n.Category, title: "Чужа яма"));
        var head = await app.SignInAsync(n.Head.Email);

        var json = await (await head.GetAsync($"/Appeals/Export?format=json&categoryId={n.Category.Id}")).Content.ReadAsStringAsync();

        Assert.Contains("Своя яма", json);
        Assert.DoesNotContain("Чужа яма", json);
    }

    [Fact]
    public async Task Notifications_open_the_appeal_and_belong_to_their_owner()
    {
        var n = await NeighbourhoodAsync();
        var appeal = await app.DataAsync(d => d.AppealAsync(n.Resident, n.Category));
        var noteId = await app.DbAsync(async (db, _) =>
        {
            var note = new Notification { UserId = n.Resident.Id, AppealId = appeal.Id, Type = NotificationType.StatusChanged, NewStatus = AppealStatus.InProgress, CreatedAt = DateTime.UtcNow, NextAttemptAt = DateTime.UtcNow };
            db.Notifications.Add(note);
            await db.SaveChangesAsync();
            return note.Id;
        });

        var stranger = await app.SignInAsync(n.Neighbour.Email);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostFormAsync($"/Notifications/MarkRead/{noteId}", [], tokenFrom: "/Notifications")).StatusCode);

        var owner = await app.SignInAsync(n.Resident.Email);
        Assert.Contains(AppealStatusName(AppealStatus.InProgress), await owner.PageAsync("/Notifications"));
        Assert.Equal($"/Appeals?appealId={appeal.Id}", (await owner.PostFormAsync($"/Notifications/MarkRead/{noteId}", [], tokenFrom: "/Notifications")).Location());
        Assert.True(await app.DbAsync((db, _) => db.Notifications.Where(x => x.Id == noteId).Select(x => x.IsRead).SingleAsync()));
    }

    [Fact]
    public async Task Rating_and_reopening_work_through_the_appeal_card()
    {
        var n = await NeighbourhoodAsync();
        var rated = await app.DataAsync(d => d.AppealAsync(n.Resident, n.Category, AppealStatus.Done));
        var reopened = await app.DataAsync(d => d.AppealAsync(n.Resident, n.Category, AppealStatus.Done));
        var client = await app.SignInAsync(n.Resident.Email);

        await client.PostFormAsync("/Appeals/Rate", new() { ["appealId"] = rated.Id.ToString(), ["rating"] = "4", ["comment"] = "Швидко" }, tokenFrom: "/Appeals");
        await client.PostFormAsync("/Appeals/Reopen", new() { ["appealId"] = reopened.Id.ToString(), ["comment"] = "Знову протікає" }, tokenFrom: "/Appeals");

        Assert.Equal(4, await app.DbAsync((db, _) => db.Appeals.Where(a => a.Id == rated.Id).Select(a => a.Rating).SingleAsync()));
        Assert.Equal(AppealStatus.InProgress, await app.DbAsync((db, _) => db.Appeals.Where(a => a.Id == reopened.Id).Select(a => a.Status).SingleAsync()));
    }

    [Fact]
    public async Task Process_ignores_a_foreign_return_url()
    {
        var n = await NeighbourhoodAsync();
        var appeal = await app.DataAsync(d => d.AppealAsync(n.Resident, n.Category));
        var head = await app.SignInAsync(n.Head.Email);
        var response = await head.PostFormAsync("/Appeals/Process", new()
        {
            ["appealId"] = appeal.Id.ToString(), ["expectedStatus"] = "New", ["status"] = "UnderReview", ["priority"] = "Normal", ["returnUrl"] = "https://evil.example"
        }, tokenFrom: "/Appeals");
        Assert.Equal($"/Appeals?appealId={appeal.Id}", response.Location());
    }
}
