using CityFix_backend.Helpers;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Tests.Infrastructure;

namespace CityFix_backend.Tests.Unit;

[Trait("Category", "Unit")]
public class TextLanguageAndTransliterationTests
{
    [Theory]
    [InlineData("Яма на дорозі", "uk")]
    [InlineData("Pothole on the road", "en")]
    [InlineData("Яма near school", "en")]
    [InlineData("", "uk")]
    [InlineData(null, "uk")]
    [InlineData("12345 !!!", "uk")]
    public void Language_is_detected_by_the_majority_alphabet(string? text, string expected)
    {
        Assert.Equal(expected, TextLanguage.Detect(text));
    }

    [Fact]
    public void Other_language_flips_uk_and_en()
    {
        Assert.Equal("en", TextLanguage.Other("uk"));
        Assert.Equal("uk", TextLanguage.Other("en"));
    }

    [Theory]
    [InlineData("Київ", "Kyiv")]
    [InlineData("Юрій", "Yurii")]
    [InlineData("Згорани", "Zghorany")]
    [InlineData("Щука", "Shchuka")]
    [InlineData("ЩУКА", "SHCHUKA")]
    [InlineData("м'ята", "miata")]
    [InlineData("Євгенія Їжакевич", "Yevheniia Yizhakevych")]
    [InlineData("Хмельницький", "Khmelnytskyi")]
    [InlineData("John Smith", "John Smith")]
    public void Names_are_transliterated_by_the_national_standard(string cyrillic, string latin)
    {
        Assert.Equal(latin, Transliteration.ToLatin(cyrillic));
    }
}

[Trait("Category", "Unit")]
public class LocalizedTests
{
    [Fact]
    public void Pick_prefers_the_current_language_and_falls_back_to_the_other()
    {
        using (new UiCulture("en"))
        {
            Assert.Equal("Roads", Localized.Pick("Дороги", "Roads"));
            Assert.Equal("Дороги", Localized.Pick("Дороги", " "));
        }
        using (new UiCulture("uk"))
        {
            Assert.Equal("Дороги", Localized.Pick("Дороги", "Roads"));
            Assert.Equal("Roads", Localized.Pick("", "Roads"));
        }
    }

    [Fact]
    public void Appeal_shows_the_translation_only_to_readers_of_the_other_language()
    {
        var appeal = new Appeal { Title = "Яма", Description = "Глибока", Language = "uk", TitleTranslated = "Pothole", DescriptionTranslated = "Deep" };
        using (new UiCulture("en"))
        {
            Assert.Equal("Pothole", appeal.DisplayTitle());
            Assert.Equal("Deep", appeal.DisplayDescription());
        }
        using (new UiCulture("uk"))
        {
            Assert.Equal("Яма", appeal.DisplayTitle());
            Assert.Equal("Глибока", appeal.DisplayDescription());
        }
    }

    [Fact]
    public void Untranslated_appeal_shows_the_original()
    {
        using var _ = new UiCulture("en");
        Assert.Equal("Яма", new Appeal { Title = "Яма", Language = "uk" }.DisplayTitle());
    }

    [Fact]
    public void Ukrainian_names_are_shown_transliterated_in_english()
    {
        var user = new User { FirstName = "Олена", LastName = "Коваль", FullNameTranslated = "Olena Koval" };
        using (new UiCulture("en")) Assert.Equal("Olena Koval", user.DisplayName());
        using (new UiCulture("uk")) Assert.Equal("Олена Коваль", user.DisplayName());
    }

    [Fact]
    public void Full_name_skips_an_empty_last_name()
    {
        Assert.Equal("Олена", new User { FirstName = "Олена", LastName = " " }.FullName);
        Assert.True(new User { Role = UserRole.AcabAdmin }.IsStaff);
        Assert.False(new User { Role = UserRole.Resident }.IsStaff);
    }
}

[Trait("Category", "Unit")]
public class UiFormatTests
{
    [Fact]
    public void Kyiv_time_follows_winter_and_summer_offsets()
    {
        Assert.Equal(new DateTime(2026, 1, 15, 12, 0, 0), new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc).ToKyiv());
        Assert.Equal(new DateTime(2026, 7, 15, 13, 0, 0), new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc).ToKyiv());
    }

    [Fact]
    public void Kyiv_day_start_is_converted_to_utc()
    {
        Assert.Equal(new DateTime(2026, 1, 14, 22, 0, 0), UiFormat.KyivDayStartUtc(new DateTime(2026, 1, 15, 17, 30, 0)));
        Assert.Equal(new DateTime(2026, 7, 14, 21, 0, 0), UiFormat.KyivDayStartUtc(new DateTime(2026, 7, 15)));
    }

    [Fact]
    public void Dates_and_coordinates_use_a_fixed_format()
    {
        var utc = new DateTime(2026, 12, 31, 23, 30, 0, DateTimeKind.Utc);
        Assert.Equal("01.01.2027", utc.ToDate());
        Assert.Equal("01.01.2027, 01:30:00", utc.ToDateTime());
        Assert.Equal("49.42161", ((decimal?)49.421614m).Coordinate());
        Assert.Equal("", ((decimal?)null).Coordinate());
    }

    [Fact]
    public void Time_ago_switches_to_a_date_after_a_week()
    {
        using var _ = new UiCulture("en");
        Assert.Equal(SharedText.Get("Time.JustNow"), DateTime.UtcNow.TimeAgo());
        Assert.Equal(DateTime.UtcNow.AddDays(-10).ToDate(), DateTime.UtcNow.AddDays(-10).TimeAgo());
    }
}

[Trait("Category", "Unit")]
public class PagingAndFilterTests
{
    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(25, 2, 2)]
    [InlineData(25, 99, 3)]
    [InlineData(25, -5, 1)]
    [InlineData(30, 3, 3)]
    public void Page_number_is_clamped_to_existing_pages(int total, int requested, int expected)
    {
        Assert.Equal(expected, PagedResult<int>.ClampPage(requested, total));
    }

    [Fact]
    public void Page_reports_its_range_and_maps_items()
    {
        var page = new PagedResult<int> { Items = [21, 22, 23], PageNumber = 3, PageSize = 10, TotalCount = 23 };
        Assert.Equal((21, 23, 3), (page.From, page.To, page.TotalPages));
        var mapped = page.Map(i => i.ToString());
        Assert.Equal(["21", "22", "23"], mapped.Items);
        Assert.Equal(23, mapped.TotalCount);
        Assert.Equal(0, new PagedResult<int>().From);
    }

    [Theory]
    [InlineData("new", "new")]
    [InlineData("closed", "closed")]
    [InlineData("bogus", "all")]
    [InlineData(null, "all")]
    public void Unknown_tabs_fall_back_to_all(string? tab, string expected)
    {
        Assert.Equal(expected, new AppealFilter { Tab = tab }.CurrentTab);
    }

    [Fact]
    public void Responsible_filter_understands_association_and_recipient_ids()
    {
        Assert.True(new AppealFilter { Responsible = "acab" }.ResponsibleIsAcab);
        Assert.Equal(7, new AppealFilter { Responsible = "7" }.ResponsibleRecipientId);
        Assert.Null(new AppealFilter { Responsible = "x" }.ResponsibleRecipientId);
    }

    [Fact]
    public void Active_filter_count_and_route_values_reflect_the_filter()
    {
        var filter = new AppealFilter
        {
            Q = "яма", From = new DateTime(2026, 1, 2), To = new DateTime(2026, 1, 5), CategoryId = 3, Responsible = "acab",
            AcabId = 4, Priority = AppealPriority.Urgent, Tab = "closed", Page = 2
        };
        Assert.Equal(5, filter.ActiveFilterCount);
        Assert.Equal(new Dictionary<string, string>
        {
            ["q"] = "яма", ["from"] = "2026-01-02", ["to"] = "2026-01-05", ["categoryId"] = "3", ["responsible"] = "acab",
            ["acabId"] = "4", ["priority"] = "Urgent", ["tab"] = "closed", ["page"] = "2"
        }, filter.ToRouteValues(includePage: true));
        Assert.DoesNotContain("tab", filter.ToRouteValues(includeTab: false).Keys);
        Assert.Empty(new AppealFilter { Responsible = "garbage" }.ToRouteValues());
    }
}

[Trait("Category", "Unit")]
public class SecurityPrimitiveTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Password_hash_verifies_only_the_right_password()
    {
        var hash = _hasher.Hash("correct horse");
        Assert.NotEqual("correct horse", hash);
        Assert.True(_hasher.Verify("correct horse", hash));
        Assert.False(_hasher.Verify("Correct horse", hash));
    }

    [Fact]
    public void Same_password_gets_a_different_salt_each_time()
    {
        Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));
    }

    [Fact]
    public void Corrupted_hash_is_a_failed_login_not_a_crash()
    {
        Assert.False(_hasher.Verify("x", "not-a-bcrypt-hash"));
        _hasher.VerifyDummy("anything");
    }

    [Fact]
    public void Generated_codes_avoid_look_alike_characters()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => RandomCodes.InvitationCode()).ToList();
        Assert.All(codes, c => Assert.DoesNotMatch("[01IO]", c));
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(Enumerable.Range(0, 50).Select(_ => RandomCodes.TemporaryPassword()), p =>
        {
            Assert.Equal(12, p.Length);
            Assert.DoesNotMatch("[01IOlo]", p);
        });
    }

    [Fact]
    public void Security_stamps_are_unique_and_fit_the_column()
    {
        var a = SecurityStamps.New();
        Assert.NotEqual(a, SecurityStamps.New());
        Assert.True(a.Length <= 32);
        Assert.NotEqual(new User().SecurityStamp, new User().SecurityStamp);
    }

    [Theory]
    [InlineData("+48 600 123 456", "+48600123456")]
    [InlineData("+1234567", null)]
    [InlineData("+1234567890123456", null)]
    [InlineData("0501234567", "+380501234567")]
    [InlineData("   ", null)]
    public void International_numbers_keep_their_country_code(string input, string? expected)
    {
        Assert.Equal(expected, PhoneNumber.ToE164(input));
    }

    [Fact]
    public void Null_email_normalises_to_empty()
    {
        Assert.Equal("", EmailAddress.Normalize(null));
    }
}

[Trait("Category", "Unit")]
public class AppealOptionsTests
{
    [Fact]
    public void Configured_deadlines_are_used()
    {
        var options = new AppealOptions { UrgentDays = 1, NormalDays = 3, LowDays = 30 };
        Assert.Equal(TimeSpan.FromDays(1), options.DueIn(AppealPriority.Urgent));
        Assert.Equal(TimeSpan.FromDays(3), options.DueIn(AppealPriority.Normal));
        Assert.Equal(TimeSpan.FromDays(30), options.DueIn(AppealPriority.Low));
        Assert.Equal(TimeSpan.FromDays(3), options.DueIn((AppealPriority)42));
    }

    [Fact]
    public void Defaults_match_the_documented_values()
    {
        var upload = new UploadOptions();
        Assert.Equal((5, 10 * 1024 * 1024L, "App_Data"), (upload.MaxPhotos, upload.MaxBytes, upload.Path));
        var appeal = new AppealOptions();
        Assert.Equal((500d, 14, 3), (appeal.SimilarRadiusMeters, appeal.FeedbackDays, appeal.MaxReopens));
    }
}
