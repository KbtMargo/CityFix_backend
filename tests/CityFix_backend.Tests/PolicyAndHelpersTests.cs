using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Reports;

namespace CityFix_backend.Tests;

public class AppealPolicyTests
{
    private static readonly User Admin = new() { Id = 1, Role = UserRole.Administrator };
    private static readonly User Head = new() { Id = 2, Role = UserRole.AcabAdmin, AcabId = 10 };
    private static readonly User OtherHead = new() { Id = 3, Role = UserRole.AcabAdmin, AcabId = 20 };
    private static readonly User Author = new() { Id = 4, Role = UserRole.Resident, AcabId = 10 };
    private static readonly User Neighbour = new() { Id = 5, Role = UserRole.Resident, AcabId = 10 };

    private static Appeal AppealBy(User author, AppealStatus status = AppealStatus.New) =>
        new() { Id = 100, AuthorId = author.Id, AcabId = author.AcabId, Status = status };

    [Fact]
    public void Residents_see_only_their_own_appeals()
    {
        var appeal = AppealBy(Author);
        Assert.True(AppealPolicy.CanView(Author, appeal));
        Assert.False(AppealPolicy.CanView(Neighbour, appeal));
    }

    [Fact]
    public void Heads_see_and_process_only_their_association()
    {
        var appeal = AppealBy(Author);
        Assert.True(AppealPolicy.CanProcess(Head, appeal));
        Assert.False(AppealPolicy.CanView(OtherHead, appeal));
        Assert.False(AppealPolicy.CanProcess(OtherHead, appeal));
        Assert.True(AppealPolicy.CanProcess(Admin, appeal));
    }

    [Fact]
    public void Author_edits_only_while_new_or_waiting_for_info()
    {
        Assert.True(AppealPolicy.CanEdit(Author, AppealBy(Author, AppealStatus.New)));
        Assert.True(AppealPolicy.CanEdit(Author, AppealBy(Author, AppealStatus.NeedsInfo)));
        Assert.False(AppealPolicy.CanEdit(Author, AppealBy(Author, AppealStatus.InProgress)));
        Assert.False(AppealPolicy.CanProcess(Author, AppealBy(Author)));
    }

    [Theory]
    [InlineData(AppealStatus.New, AppealStatus.InProgress, true)]
    [InlineData(AppealStatus.InProgress, AppealStatus.Done, true)]
    [InlineData(AppealStatus.New, AppealStatus.Done, false)]
    [InlineData(AppealStatus.Done, AppealStatus.InProgress, false)]
    [InlineData(AppealStatus.Rejected, AppealStatus.New, false)]
    public void Workflow_allows_only_listed_transitions(AppealStatus from, AppealStatus to, bool allowed)
    {
        Assert.Equal(allowed, AppealWorkflow.CanMove(from, to));
    }
}

public class HelperTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\")", "\"'=HYPERLINK(\"\"http://evil\"\")\"")]
    [InlineData("+38050", "'+38050")]
    [InlineData("-1+1", "'-1+1")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("Яма, біля школи", "\"Яма, біля школи\"")]
    [InlineData("Plain", "Plain")]
    public void Csv_cells_cannot_become_formulas(string value, string expected)
    {
        Assert.Equal(expected, CsvCell.Format(value));
    }

    [Fact]
    public void Csv_keeps_negative_numbers_as_numbers()
    {
        Assert.Equal("-26.5", CsvCell.Format(-26.5m));
    }

    [Theory]
    [InlineData("050 123 45 67", "+380501234567")]
    [InlineData("+38 (050) 123-45-67", "+380501234567")]
    [InlineData("380501234567", "+380501234567")]
    [InlineData("12345", null)]
    [InlineData("", null)]
    public void Phone_numbers_are_normalised_to_E164(string input, string? expected)
    {
        Assert.Equal(expected, PhoneNumber.ToE164(input));
    }

    [Fact]
    public void Search_pattern_escapes_like_wildcards()
    {
        Assert.Equal("%100\\%\\_a\\\\b%", Search.ContainsPattern(" 100%_a\\b "));
    }

    [Fact]
    public void Emails_are_compared_case_insensitively()
    {
        Assert.Equal("user@example.com", EmailAddress.Normalize("  User@Example.COM "));
    }

    [Fact]
    public void Invitation_codes_ignore_spaces_and_case()
    {
        Assert.Equal("ABCD2345", RandomCodes.NormalizeCode(" abcd 2345 "));
        Assert.Equal(10, RandomCodes.InvitationCode().Length);
    }
}
