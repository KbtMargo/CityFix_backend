using CityFix_backend.Helpers;
using CityFix_backend.Models;
using CityFix_backend.Tests.Infrastructure;

namespace CityFix_backend.Tests.Unit;

[Trait("Category", "Unit")]
public class EmailTemplateTests
{
    private static EmailTemplate Letter(string? button = "https://cityfix.example/confirm?token=a&b=1") => new()
    {
        Subject = "Тема <b>",
        Preheader = "Короткий текст",
        Heading = "Вітаємо, <script>alert(1)</script>",
        Paragraphs = { "Перший абзац & ще" },
        QuoteLabel = "Коментар",
        Quote = "\"<img src=x onerror=alert(1)>\"",
        ButtonText = button == null ? null : "Підтвердити",
        ButtonUrl = button,
        Notes = { "Посилання діє 48 год." }
    };

    [Fact]
    public void Html_version_encodes_everything_that_came_from_users()
    {
        var html = Letter().Render().Html;
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("Перший абзац &amp; ще", html);
        Assert.Contains("href=\"https://cityfix.example/confirm?token=a&amp;b=1\"", html);
    }

    [Fact]
    public void Text_version_has_all_parts_and_the_plain_link()
    {
        var mail = Letter().Render();
        Assert.Equal("Тема <b>", mail.Subject);
        Assert.Contains("Вітаємо, <script>alert(1)</script>", mail.Text);
        Assert.Contains("Коментар: «\"<img src=x onerror=alert(1)>\"»", mail.Text);
        Assert.Contains("Підтвердити: https://cityfix.example/confirm?token=a&b=1", mail.Text);
        Assert.Contains("Посилання діє 48 год.", mail.Text);
    }

    [Fact]
    public void Letter_without_a_link_has_no_button()
    {
        var mail = Letter(button: null).Render();
        Assert.DoesNotContain("<a href", mail.Html);
        Assert.DoesNotContain("http", mail.Text);
    }

    [Fact]
    public void Greeting_uses_the_name_when_known()
    {
        using var _ = new UiCulture("en");
        Assert.Contains("Olena", EmailTemplate.Greeting("Olena"));
        Assert.Equal(SharedText.Get("Email.GreetingNoName"), EmailTemplate.Greeting(" "));
    }

    [Fact]
    public void Html_declares_the_reader_language()
    {
        using (new UiCulture("en")) Assert.Contains("<html lang=\"en\">", Letter().Render().Html);
        using (new UiCulture("uk")) Assert.Contains("<html lang=\"uk\">", Letter().Render().Html);
    }
}

[Trait("Category", "Unit")]
public class NotificationTextTests
{
    private static readonly Appeal Appeal = new() { Id = 5, Title = "Яма", AuthorId = 1, Language = "uk", Rating = 4 };

    private static Notification Note(NotificationType type, AppealStatus? status = null, string message = "", int userId = 1) =>
        new() { Type = type, NewStatus = status, Message = message, Appeal = Appeal, UserId = userId };

    [Fact]
    public void Every_type_has_its_own_title()
    {
        using var _ = new UiCulture("uk");
        var titles = new[]
        {
            NotificationText.Title(Note(NotificationType.StatusChanged, AppealStatus.Done)),
            NotificationText.Title(Note(NotificationType.StatusChanged)),
            NotificationText.Title(Note(NotificationType.NewAppeal)),
            NotificationText.Title(Note(NotificationType.Assigned)),
            NotificationText.Title(Note(NotificationType.Reopened)),
            NotificationText.Title(Note(NotificationType.Rated)),
            NotificationText.Title(Note(NotificationType.Overdue)),
            NotificationText.Title(Note(NotificationType.StatusChanged, AppealStatus.Done, userId: 2))
        };
        Assert.Equal(titles.Length, titles.Distinct().Count());
        Assert.DoesNotContain(titles, t => t.StartsWith("Notifications.", StringComparison.Ordinal));
    }

    [Fact]
    public void Status_change_names_the_appeal_and_the_new_status()
    {
        using var _ = new UiCulture("en");
        var text = NotificationText.Text(Note(NotificationType.StatusChanged, AppealStatus.Done, "Repaired"));
        Assert.Contains("Яма", text);
        Assert.Contains(AppealStatus.Done.GetDisplayName(), text);
        Assert.EndsWith("Repaired", text);
    }

    [Fact]
    public void Supporters_get_no_comment_meant_for_the_author()
    {
        var forSupporter = Note(NotificationType.StatusChanged, AppealStatus.Done, "Особисто для автора", userId: 2);
        Assert.True(NotificationText.IsForSupporter(forSupporter));
        Assert.Null(NotificationText.Comment(forSupporter));
        Assert.Equal("Особисто для автора", NotificationText.Comment(Note(NotificationType.StatusChanged, AppealStatus.Done, "Особисто для автора")));
    }

    [Fact]
    public void Assignment_uses_the_responsible_name_in_the_reader_language()
    {
        var note = Note(NotificationType.Assigned, message: "Водоканал");
        note.MessageEn = "Water utility";
        using (new UiCulture("en")) Assert.Contains("Water utility", NotificationText.Lead(note));
        using (new UiCulture("uk")) Assert.Contains("Водоканал", NotificationText.Lead(note));
        Assert.Null(NotificationText.Comment(note));
    }

    [Fact]
    public void Rating_notification_shows_the_stars()
    {
        using var _ = new UiCulture("en");
        Assert.Contains("4", NotificationText.Lead(Note(NotificationType.Rated)));
    }
}
