using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CityFix_backend.Tests.Integration;

[Trait("Category", "Integration")]
public class UserServiceTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>, IAsyncLifetime
{
    private ServiceHarness _app = null!;
    private IUserService Users => _app.Get<IUserService>();

    public Task InitializeAsync()
    {
        _app = new ServiceHarness(database);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private async Task<User> ReloadAsync(int id)
    {
        _app.NewScope();
        return await _app.Db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
    }

    [Fact]
    public async Task Administrator_creates_users_with_a_one_time_password()
    {
        var admin = await _app.Data.AdminAsync();
        var acab = await _app.Data.AcabAsync();
        var email = TestData.UniqueEmail("Head");

        var (user, password) = await Users.CreateAsync(new UserInput("Ірина", null, email, null, UserRole.AcabAdmin, acab.Id), admin);

        Assert.Equal(12, password.Length);
        var stored = await ReloadAsync(user.Id);
        Assert.Equal((email.ToLowerInvariant(), true, true, UserRole.AcabAdmin), (stored.Email, stored.MustChangePassword, stored.EmailConfirmed, stored.Role));
        Assert.True(await _app.Get<IAuthService>().VerifyPasswordAsync(user.Id, password));
    }

    [Fact]
    public async Task Only_administrators_manage_users()
    {
        var head = await _app.Data.HeadAsync(await _app.Data.AcabAsync());
        var victim = await _app.Data.UserAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => Users.CreateAsync(new UserInput("X", null, TestData.UniqueEmail(), null, UserRole.Resident, null), head));
        await Assert.ThrowsAsync<ForbiddenException>(() => Users.ResetPasswordAsync(victim.Id, head));
        await Assert.ThrowsAsync<ForbiddenException>(() => Users.SetBlockedAsync(victim.Id, true, head));
        await Assert.ThrowsAsync<ForbiddenException>(() => Users.DeleteAsync(victim.Id, head));
        await Assert.ThrowsAsync<ForbiddenException>(() => Users.GetPageAsync(victim, null, 1));
    }

    [Fact]
    public async Task Head_must_belong_to_an_association_and_the_association_must_exist()
    {
        var admin = await _app.Data.AdminAsync();
        var noAcab = await Assert.ThrowsAsync<ServiceValidationException>(() =>
            Users.CreateAsync(new UserInput("X", null, TestData.UniqueEmail(), null, UserRole.AcabAdmin, null), admin));
        Assert.Equal("Validation.HeadNeedsAcab", noAcab.MessageKey);
        var missing = await Assert.ThrowsAsync<ServiceValidationException>(() =>
            Users.CreateAsync(new UserInput("X", null, TestData.UniqueEmail(), null, UserRole.Resident, 999_999), admin));
        Assert.Equal("AcabId", missing.Field);
        await Assert.ThrowsAsync<ServiceValidationException>(() =>
            Users.CreateAsync(new UserInput("X", null, TestData.UniqueEmail(), null, (UserRole)42, null), admin));
    }

    [Fact]
    public async Task Emails_are_unique_regardless_of_case()
    {
        var admin = await _app.Data.AdminAsync();
        var existing = await _app.Data.UserAsync();
        var ex = await Assert.ThrowsAsync<ServiceValidationException>(() =>
            Users.CreateAsync(new UserInput("X", null, existing.Email.ToUpperInvariant(), null, UserRole.Resident, null), admin));
        Assert.Equal(("Email", "Auth.EmailTaken"), (ex.Field, ex.MessageKey));
    }

    [Fact]
    public async Task The_last_active_administrator_cannot_be_removed()
    {
        // Administrators are counted across the whole database, so this test gets its own.
        var isolated = new PostgresDatabase();
        await isolated.InitializeAsync();
        try
        {
            await using var app = new ServiceHarness(isolated);
            var admin = await app.Data.AdminAsync();
            var other = await app.Data.AdminAsync();
            var asResident = new UserInput("A", null, admin.Email, null, UserRole.Resident, null);

            await app.Get<IUserService>().SetBlockedAsync(other.Id, true, admin);

            app.NewScope();
            Assert.Equal("Users.LastAdmin", (await Assert.ThrowsAsync<ServiceValidationException>(() => app.Get<IUserService>().SetBlockedAsync(admin.Id, true, other))).MessageKey);
            app.NewScope();
            Assert.Equal("Users.LastAdmin", (await Assert.ThrowsAsync<ServiceValidationException>(() => app.Get<IUserService>().UpdateAsync(admin.Id, asResident, other))).MessageKey);
            app.NewScope();
            Assert.Equal("Users.CannotDemoteSelf", (await Assert.ThrowsAsync<ServiceValidationException>(() => app.Get<IUserService>().UpdateAsync(admin.Id, asResident, admin))).MessageKey);
            app.NewScope();
            Assert.Equal("Users.LastAdmin", (await Assert.ThrowsAsync<ServiceValidationException>(() => app.Get<IUserService>().AnonymizeAsync(admin.Id))).MessageKey);

            app.NewScope();
            await app.Get<IUserService>().SetBlockedAsync(other.Id, false, admin);
            app.NewScope();
            await app.Get<IUserService>().UpdateAsync(admin.Id, asResident, other);
        }
        finally
        {
            await isolated.DisposeAsync();
        }
    }

    [Fact]
    public async Task Administrators_cannot_block_themselves()
    {
        var admin = await _app.Data.AdminAsync();
        var ex = await Assert.ThrowsAsync<ServiceValidationException>(() => Users.SetBlockedAsync(admin.Id, true, admin));
        Assert.Equal("Users.CannotBlockSelf", ex.MessageKey);
    }

    [Fact]
    public async Task Users_with_appeals_or_history_are_kept_others_can_be_deleted()
    {
        var admin = await _app.Data.AdminAsync();
        var author = await _app.Data.UserAsync();
        await _app.Data.AppealAsync(author, await _app.Data.CategoryAsync());
        var idle = await _app.Data.UserAsync();

        var check = await Users.CheckDeleteAsync(author.Id, admin);
        Assert.Equal(("Users.CannotDeleteText", 1), (check.BlockedKey, check.UsageCount));
        await Assert.ThrowsAsync<ServiceValidationException>(() => Users.DeleteAsync(author.Id, admin));
        Assert.Equal("Users.CannotDeleteSelfText", (await Users.CheckDeleteAsync(admin.Id, admin)).BlockedKey);

        await Users.DeleteAsync(idle.Id, admin);
        Assert.False(await _app.Db.Users.AnyAsync(u => u.Id == idle.Id));
    }

    [Fact]
    public async Task Admin_reset_issues_a_new_password_and_ends_sessions()
    {
        var admin = await _app.Data.AdminAsync();
        var user = await _app.Data.UserAsync();
        var password = await Users.ResetPasswordAsync(user.Id, admin);
        var stored = await ReloadAsync(user.Id);
        Assert.True(stored.MustChangePassword);
        Assert.NotEqual(user.SecurityStamp, stored.SecurityStamp);
        Assert.True(await _app.Get<IAuthService>().VerifyPasswordAsync(user.Id, password));
    }

    [Fact]
    public async Task Admin_email_change_is_applied_immediately_and_resets_the_translated_name()
    {
        var admin = await _app.Data.AdminAsync();
        var user = await _app.Data.UserAsync();
        await _app.Db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.FullNameTranslated, "Taras Shevchenko"));
        var email = TestData.UniqueEmail("renamed");

        await Users.UpdateAsync(user.Id, new UserInput("Іван", "Франко", email, "", UserRole.Resident, null), admin);

        var stored = await ReloadAsync(user.Id);
        Assert.Equal((email, "Іван", "Франко", null, null), (stored.Email, stored.FirstName, stored.LastName, stored.Phone, stored.FullNameTranslated));
    }

    [Fact]
    public async Task Head_lists_only_members_of_own_association()
    {
        var n = await _app.Data.NeighbourhoodAsync();
        await _app.Data.UserAsync();

        var page = await Users.GetPageAsync(n.Head, null, 1);

        Assert.Equal([n.Head.Id, n.Resident.Id, n.Neighbour.Id], page.Page.Items.Select(r => r.User.Id).Order());
        Assert.Equal(n.Acab.Id, page.OwnAcab!.Id);
        Assert.Equal("Users.CannotDeleteSelf", page.Page.Items.Single(r => r.User.Id == n.Head.Id).DeleteBlockedKey);
    }

    [Fact]
    public async Task Administrator_searches_users_by_name_or_email()
    {
        var admin = await _app.Data.AdminAsync();
        var marker = "Zx" + Guid.NewGuid().ToString("N")[..6];
        var found = await _app.Data.UserAsync(firstName: marker);
        var acab = await _app.Data.AcabAsync();
        var member = await _app.Data.UserAsync(acab: acab);

        Assert.Equal(found.Id, Assert.Single((await Users.GetPageAsync(admin, marker.ToLowerInvariant(), 1)).Page.Items).User.Id);
        Assert.Single((await Users.GetPageAsync(admin, found.Email[..20], 1)).Page.Items);
        Assert.Empty((await Users.GetPageAsync(admin, "%", 1)).Page.Items.Where(r => !r.User.Email.Contains('%')));
        Assert.Equal(member.Id, Assert.Single((await Users.GetPageAsync(admin, null, 1, acab.Id)).Page.Items).User.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => Users.GetPageAsync(admin, null, 1, 999_999));
    }

    [Fact]
    public async Task Profiles_are_visible_to_self_own_head_and_administrators_only()
    {
        var n = await _app.Data.NeighbourhoodAsync();
        var stranger = await _app.Data.HeadAsync(await _app.Data.AcabAsync());
        Assert.Equal(n.Resident.Id, (await Users.GetForViewAsync(n.Resident.Id, n.Resident)).Id);
        await Users.GetForViewAsync(n.Resident.Id, n.Head);
        await Assert.ThrowsAsync<ForbiddenException>(() => Users.GetForViewAsync(n.Resident.Id, n.Neighbour));
        await Assert.ThrowsAsync<ForbiddenException>(() => Users.GetForViewAsync(n.Resident.Id, stranger));
        await Assert.ThrowsAsync<NotFoundException>(() => Users.GetForViewAsync(999_999, n.Resident));
    }

    [Fact]
    public async Task Changing_own_email_waits_for_confirmation()
    {
        var user = await _app.Data.UserAsync();
        var wanted = TestData.UniqueEmail("next");

        Assert.Equal(wanted, await Users.UpdateProfileAsync(user.Id, new ProfileInput("Тарас", null, wanted.ToUpperInvariant(), null)));
        Assert.Null(await Users.UpdateProfileAsync(user.Id, new ProfileInput("Тарас", null, wanted, null)));
        var stored = await ReloadAsync(user.Id);
        Assert.Equal((user.Email, wanted), (stored.Email, stored.PendingEmail));

        Assert.Null(await Users.UpdateProfileAsync(user.Id, new ProfileInput("Тарас", null, user.Email, null)));
        Assert.Null((await ReloadAsync(user.Id)).PendingEmail);
    }

    [Fact]
    public async Task Profile_cannot_take_someone_elses_email()
    {
        var user = await _app.Data.UserAsync();
        var other = await _app.Data.UserAsync();
        await Assert.ThrowsAsync<ServiceValidationException>(() => Users.UpdateProfileAsync(user.Id, new ProfileInput("X", null, other.Email, null)));
    }

    [Fact]
    public async Task Notification_settings_and_language_are_saved()
    {
        var user = await _app.Data.UserAsync();
        await Users.UpdateNotificationSettingsAsync(user.Id, new NotificationSettings(false, true));
        await Users.SetLanguageAsync(user.Id, "en");
        var stored = await ReloadAsync(user.Id);
        Assert.Equal((false, true, "en"), (stored.NotifyInApp, stored.NotifyEmail, stored.Language));

        await Users.SetLanguageAsync(user.Id, "de");
        Assert.Equal("uk", (await ReloadAsync(user.Id)).Language);
        await Assert.ThrowsAsync<NotFoundException>(() => Users.UpdateNotificationSettingsAsync(999_999, new NotificationSettings(true, true)));
    }

    [Fact]
    public async Task Profile_statistics_count_own_appeals_supports_and_unread_notifications()
    {
        var n = await _app.Data.NeighbourhoodAsync();
        var otherHead = await _app.Data.HeadAsync(n.Acab);
        await _app.Data.AppealAsync(n.Resident, n.Category);
        await _app.Data.AppealAsync(n.Resident, n.Category, AppealStatus.InProgress);
        var done = await _app.Data.AppealAsync(n.Resident, n.Category, AppealStatus.Done);
        var neighbours = await _app.Data.AppealAsync(n.Neighbour, n.Category);
        _app.Db.AppealSupports.Add(new AppealSupport { AppealId = neighbours.Id, UserId = n.Resident.Id, CreatedAt = DateTime.UtcNow });
        _app.Db.Notifications.Add(new Notification { UserId = n.Resident.Id, AppealId = done.Id, CreatedAt = DateTime.UtcNow, NextAttemptAt = DateTime.UtcNow });
        await _app.Db.SaveChangesAsync();

        var stats = await Users.GetProfileStatsAsync(n.Resident.Id);

        Assert.Equal(new ProfileStats(3, 2, 1, 1, 1, 2, 2), stats);
        Assert.Equal(1, (await Users.GetProfileStatsAsync(otherHead.Id)).OtherHeads);
    }

    [Fact]
    public async Task Personal_data_export_contains_profile_appeals_history_and_notifications()
    {
        var n = await _app.Data.NeighbourhoodAsync();
        var appeal = await _app.Data.AppealAsync(n.Resident, n.Category, title: "Мій ліхтар");
        _app.Db.AppealUpdates.Add(new AppealUpdate { AppealId = appeal.Id, OldStatus = AppealStatus.New, NewStatus = AppealStatus.InProgress, ChangedById = n.Head.Id, Comment = "Беремо", ChangedAt = DateTime.UtcNow });
        _app.Db.Notifications.Add(new Notification { UserId = n.Resident.Id, AppealId = appeal.Id, Message = "Беремо", CreatedAt = DateTime.UtcNow, NextAttemptAt = DateTime.UtcNow });
        await _app.Db.SaveChangesAsync();

        var export = await Users.ExportPersonalDataAsync(n.Resident.Id);

        Assert.Equal((n.Resident.Email, n.Acab.Name), (export.Profile.Email, export.Profile.Association));
        var exported = Assert.Single(export.Appeals);
        Assert.Equal("Мій ліхтар", exported.Title);
        Assert.Equal("Беремо", Assert.Single(exported.History).Comment);
        Assert.Single(export.Notifications);
    }

    [Fact]
    public async Task Self_deletion_anonymises_the_account_and_keeps_appeals()
    {
        var n = await _app.Data.NeighbourhoodAsync();
        var appeal = await _app.Data.AppealAsync(n.Resident, n.Category);
        _app.Db.Notifications.Add(new Notification { UserId = n.Resident.Id, CreatedAt = DateTime.UtcNow, NextAttemptAt = DateTime.UtcNow });
        await _app.Db.SaveChangesAsync();

        await Users.AnonymizeAsync(n.Resident.Id);

        var stored = await ReloadAsync(n.Resident.Id);
        Assert.Equal($"deleted-{n.Resident.Id}@deleted.invalid", stored.Email);
        Assert.Equal((null, null, null, true), (stored.LastName, stored.Phone, stored.AcabId, stored.IsBlocked));
        Assert.DoesNotContain(n.Resident.FirstName, stored.FirstName);
        Assert.False(await _app.Db.Notifications.AnyAsync(x => x.UserId == n.Resident.Id));
        Assert.True(await _app.Db.Appeals.AnyAsync(a => a.Id == appeal.Id));
        Assert.Null((await _app.Get<IAuthService>().LoginAsync(n.Resident.Email, TestData.Password)).User);
    }

    [Fact]
    public async Task Blocked_or_missing_actor_is_rejected()
    {
        var blocked = await _app.Data.UserAsync(blocked: true);
        Assert.Null(await Users.FindActorAsync(blocked.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => Users.GetActorAsync(blocked.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => Users.GetActorAsync(999_999));
    }
}

[Trait("Category", "Integration")]
public class AcabServiceTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>, IAsyncLifetime
{
    private ServiceHarness _app = null!;
    private IAcabService Acabs => _app.Get<IAcabService>();

    public Task InitializeAsync()
    {
        _app = new ServiceHarness(database);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task New_association_gets_two_different_invitation_codes()
    {
        var acab = await Acabs.CreateAsync(new AcabInput("  ОСББ «Затишок»  ", " ", " Zatyshok ", null));
        Assert.Equal(("ОСББ «Затишок»", null, "Zatyshok", null), (acab.Name, acab.Address, acab.NameEn, acab.AddressEn));
        Assert.Equal(10, acab.ResidentCode.Length);
        Assert.NotEqual(acab.ResidentCode, acab.HeadCode);
    }

    [Fact]
    public async Task Codes_are_found_whatever_the_spacing_or_case()
    {
        var acab = await _app.Data.AcabAsync();
        var spaced = string.Join(" ", acab.ResidentCode.Chunk(5).Select(c => new string(c))).ToLowerInvariant();

        Assert.Equal((acab.Id, UserRole.Resident), ((await Acabs.FindByCodeAsync(spaced))!.Value.Acab.Id, (await Acabs.FindByCodeAsync(spaced))!.Value.Role));
        Assert.Equal(UserRole.AcabAdmin, (await Acabs.FindByCodeAsync(acab.HeadCode))!.Value.Role);
        Assert.Null(await Acabs.FindByCodeAsync("   "));
        Assert.Null(await Acabs.FindByCodeAsync("ZZZZZZZZZZ"));
    }

    [Fact]
    public async Task Regenerated_code_replaces_the_old_one()
    {
        var acab = await _app.Data.AcabAsync();
        var code = await Acabs.RegenerateCodeAsync(acab.Id, AcabCodeKind.Resident);
        Assert.NotEqual(acab.ResidentCode, code);
        Assert.Null(await Acabs.FindByCodeAsync(acab.ResidentCode));
        Assert.Equal(acab.Id, (await Acabs.FindByCodeAsync(code))!.Value.Acab.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => Acabs.RegenerateCodeAsync(999_999, AcabCodeKind.Head));
    }

    [Fact]
    public async Task Head_code_can_be_consumed_only_once_even_concurrently()
    {
        var acab = await _app.Data.AcabAsync();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            _app.InScopeAsync(sp => sp.GetRequiredService<IAcabService>().ConsumeHeadCodeAsync(acab.Id, acab.HeadCode))));
        Assert.Equal(1, attempts.Count(ok => ok));
    }

    [Fact]
    public async Task Associations_in_use_cannot_be_deleted()
    {
        var withMember = await _app.Data.AcabAsync();
        await _app.Data.UserAsync(acab: withMember);
        Assert.Equal("Acabs.CannotDeleteText", (await Acabs.CheckDeleteAsync(withMember.Id)).BlockedKey);
        await Assert.ThrowsAsync<ServiceValidationException>(() => Acabs.DeleteAsync(withMember.Id));

        var empty = await _app.Data.AcabAsync();
        await Acabs.DeleteAsync(empty.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => Acabs.GetAsync(empty.Id));
    }

    [Fact]
    public async Task List_counts_members_heads_and_appeals()
    {
        var n = await _app.Data.NeighbourhoodAsync();
        await _app.Data.AppealAsync(n.Resident, n.Category);

        var row = Assert.Single((await Acabs.GetPageAsync(n.Acab.Name, 1)).Items);
        Assert.Equal((3, 1, 1, false), (row.MemberCount, row.HeadCount, row.AppealCount, row.CanDelete));
    }

    [Fact]
    public async Task Update_trims_and_clears_blank_fields()
    {
        var acab = await _app.Data.AcabAsync();
        await Acabs.UpdateAsync(acab.Id, new AcabInput(" Нова назва ", "", "New name", "  "));
        _app.NewScope();
        var stored = await Acabs.GetAsync(acab.Id);
        Assert.Equal(("Нова назва", null, "New name", null), (stored.Name, stored.Address, stored.NameEn, stored.AddressEn));
    }
}
