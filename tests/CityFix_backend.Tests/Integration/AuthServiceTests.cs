using System.Security.Claims;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CityFix_backend.Tests.Integration;

[Trait("Category", "Integration")]
public class AuthServiceTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>, IAsyncLifetime
{
    private ServiceHarness _app = null!;
    private IAuthService Auth => _app.Get<IAuthService>();

    public Task InitializeAsync()
    {
        _app = new ServiceHarness(database);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private static RegisterRequest Request(string? email = null, string? code = null) =>
        new(email ?? TestData.UniqueEmail("new"), "Secret123", " Олена ", " Коваль ", " 050 123 45 67 ", code);

    private async Task<User> ReloadAsync(int id)
    {
        _app.NewScope();
        return await _app.Db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
    }

    [Fact]
    public async Task Registration_without_a_code_creates_an_unconfirmed_resident()
    {
        var email = TestData.UniqueEmail("New");
        var result = await Auth.RegisterAsync(Request(email.ToUpperInvariant()));

        Assert.True(result.Succeeded);
        var user = await ReloadAsync(result.User!.Id);
        Assert.Equal((email.ToLowerInvariant(), UserRole.Resident, null, false), (user.Email, user.Role, user.AcabId, user.EmailConfirmed));
        Assert.Equal(("Олена", "Коваль", "050 123 45 67"), (user.FirstName, user.LastName, user.Phone));
        Assert.NotEqual("Secret123", user.PasswordHash);
    }

    [Fact]
    public async Task Resident_code_joins_the_association_as_a_resident()
    {
        var acab = await _app.Data.AcabAsync();
        var result = await Auth.RegisterAsync(Request(code: acab.ResidentCode.ToLowerInvariant()));
        Assert.Equal((UserRole.Resident, acab.Id), (result.User!.Role, result.User.AcabId));
        Assert.Equal(acab.ResidentCode, (await _app.Db.Acabs.AsNoTracking().SingleAsync(a => a.Id == acab.Id)).ResidentCode);
    }

    [Fact]
    public async Task Head_code_makes_a_head_and_works_only_once()
    {
        var acab = await _app.Data.AcabAsync();

        var first = await Auth.RegisterAsync(Request(code: acab.HeadCode));
        Assert.Equal((UserRole.AcabAdmin, acab.Id), (first.User!.Role, first.User.AcabId));

        _app.NewScope();
        var second = await Auth.RegisterAsync(Request(code: acab.HeadCode));
        Assert.False(second.Succeeded);
        Assert.Equal("Auth.CodeNotFound", second.Errors["InvitationCode"]);
        Assert.NotEqual(acab.HeadCode, (await _app.Db.Acabs.AsNoTracking().SingleAsync(a => a.Id == acab.Id)).HeadCode);
    }

    [Fact]
    public async Task Unknown_code_is_reported_on_the_code_field()
    {
        var result = await Auth.RegisterAsync(Request(code: "NOSUCHCODE"));
        Assert.Equal("Auth.CodeNotFound", result.Errors["InvitationCode"]);
    }

    [Fact]
    public async Task Registering_a_confirmed_email_again_does_not_reveal_or_duplicate_the_account()
    {
        var existing = await _app.Data.UserAsync();
        var result = await Auth.RegisterAsync(Request(existing.Email));

        Assert.False(result.Succeeded);
        Assert.Empty(result.Errors);
        Assert.Equal(existing.Id, result.ExistingAccount!.Id);
        Assert.Equal(1, await _app.Db.Users.CountAsync(u => u.Email == existing.Email));
    }

    [Fact]
    public async Task Registering_again_replaces_an_unconfirmed_account()
    {
        var abandoned = await _app.Data.UserAsync(confirmed: false);
        var result = await Auth.RegisterAsync(Request(abandoned.Email));

        Assert.True(result.Succeeded);
        Assert.NotEqual(abandoned.Id, result.User!.Id);
        Assert.False(await _app.Db.Users.AnyAsync(u => u.Id == abandoned.Id));
    }

    [Fact]
    public async Task Login_works_with_any_email_case_and_the_right_password()
    {
        var user = await _app.Data.UserAsync();
        var login = await Auth.LoginAsync("  " + user.Email.ToUpperInvariant(), TestData.Password);
        Assert.Equal(user.Id, login.User!.Id);
        Assert.Null((await Auth.LoginAsync(user.Email, "wrong")).User);
        Assert.Null((await Auth.LoginAsync("nobody@test.local", TestData.Password)).User);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_for_fifteen_minutes()
    {
        var user = await _app.Data.UserAsync();
        for (var i = 0; i < AuthService.MaxFailedLogins; i++)
        {
            Assert.False((await Auth.LoginAsync(user.Email, "wrong")).LockedOut);
        }

        var locked = await Auth.LoginAsync(user.Email, TestData.Password);
        Assert.True(locked.LockedOut);
        Assert.Null(locked.User);

        _app.Clock.Advance(AuthService.LockoutDuration + TimeSpan.FromSeconds(1));
        _app.NewScope();
        Assert.Equal(user.Id, (await Auth.LoginAsync(user.Email, TestData.Password)).User!.Id);
        var reloaded = await ReloadAsync(user.Id);
        Assert.Equal((0, (DateTime?)null), (reloaded.FailedLoginCount, reloaded.LockoutEnd));
    }

    [Fact]
    public async Task Successful_login_resets_the_failure_counter()
    {
        var user = await _app.Data.UserAsync();
        await Auth.LoginAsync(user.Email, "wrong");
        await Auth.LoginAsync(user.Email, "wrong");
        Assert.Equal(2, (await ReloadAsync(user.Id)).FailedLoginCount);

        await Auth.LoginAsync(user.Email, TestData.Password);
        Assert.Equal(0, (await ReloadAsync(user.Id)).FailedLoginCount);
    }

    [Fact]
    public async Task Blocked_users_cannot_sign_in()
    {
        var user = await _app.Data.UserAsync(blocked: true);
        var login = await Auth.LoginAsync(user.Email, TestData.Password);
        Assert.Equal((null, false), (login.User, login.LockedOut));
    }

    [Fact]
    public async Task Confirmation_token_confirms_the_email_once()
    {
        var user = await _app.Data.UserAsync(confirmed: false);
        var token = Auth.CreateEmailToken(user.Id, user.Email);

        Assert.Equal(EmailConfirmation.Confirmed, (await Auth.ConfirmEmailAsync(token)).Result);
        Assert.True((await ReloadAsync(user.Id)).EmailConfirmed);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("")]
    public async Task Forged_tokens_are_rejected(string token)
    {
        Assert.Equal(EmailConfirmation.Invalid, (await Auth.ConfirmEmailAsync(token)).Result);
        Assert.Null(await Auth.FindByResetTokenAsync(token));
    }

    [Fact]
    public async Task Token_for_an_old_address_does_not_confirm_the_new_one()
    {
        var user = await _app.Data.UserAsync(confirmed: false);
        var token = Auth.CreateEmailToken(user.Id, "someone-else@test.local");
        Assert.Equal(EmailConfirmation.Invalid, (await Auth.ConfirmEmailAsync(token)).Result);
    }

    [Fact]
    public async Task Confirming_a_pending_email_switches_the_address()
    {
        var user = await _app.Data.UserAsync();
        var newEmail = TestData.UniqueEmail("moved");
        await _app.Get<IUserService>().UpdateProfileAsync(user.Id, new ProfileInput(user.FirstName, user.LastName, newEmail, null));

        _app.NewScope();
        var (result, _) = await Auth.ConfirmEmailAsync(Auth.CreateEmailToken(user.Id, newEmail));

        Assert.Equal(EmailConfirmation.Confirmed, result);
        var reloaded = await ReloadAsync(user.Id);
        Assert.Equal((newEmail, (string?)null), (reloaded.Email, reloaded.PendingEmail));
    }

    [Fact]
    public async Task Pending_email_taken_meanwhile_is_reported()
    {
        var user = await _app.Data.UserAsync();
        var wanted = TestData.UniqueEmail("wanted");
        await _app.Get<IUserService>().UpdateProfileAsync(user.Id, new ProfileInput(user.FirstName, null, wanted, null));
        await _app.Data.UserAsync(email: wanted);

        _app.NewScope();
        Assert.Equal(EmailConfirmation.EmailTaken, (await Auth.ConfirmEmailAsync(Auth.CreateEmailToken(user.Id, wanted))).Result);
    }

    [Fact]
    public async Task Password_reset_token_works_once_and_signs_out_other_sessions()
    {
        var user = await _app.Data.UserAsync();
        await Auth.LoginAsync(user.Email, "wrong");
        var token = Auth.CreatePasswordResetToken(user);

        Assert.Equal(user.Id, (await Auth.FindByResetTokenAsync(token))!.Id);
        Assert.True(await Auth.ResetPasswordAsync(token, "BrandNew123"));

        _app.NewScope();
        var reloaded = await ReloadAsync(user.Id);
        Assert.NotEqual(user.SecurityStamp, reloaded.SecurityStamp);
        Assert.Equal(0, reloaded.FailedLoginCount);
        Assert.NotNull((await Auth.LoginAsync(user.Email, "BrandNew123")).User);
        Assert.False(await Auth.ResetPasswordAsync(token, "Again12345"));
    }

    [Fact]
    public async Task Reset_tokens_are_not_issued_or_accepted_for_blocked_users()
    {
        var user = await _app.Data.UserAsync(blocked: true);
        Assert.Null(await Auth.FindForPasswordResetAsync(user.Email));
        Assert.Null(await Auth.FindByResetTokenAsync(Auth.CreatePasswordResetToken(user)));
    }

    [Fact]
    public async Task Changing_the_password_requires_the_current_one()
    {
        var user = await _app.Data.UserAsync(mustChangePassword: true);
        Assert.Null(await Auth.ChangePasswordAsync(user.Id, "wrong", "NewPassword1"));

        var changed = await Auth.ChangePasswordAsync(user.Id, TestData.Password, "NewPassword1");
        Assert.NotNull(changed);
        Assert.False(changed.MustChangePassword);
        Assert.NotEqual(user.SecurityStamp, changed.SecurityStamp);
        Assert.True(await Auth.VerifyPasswordAsync(user.Id, "NewPassword1"));
        Assert.False(await Auth.VerifyPasswordAsync(user.Id, TestData.Password));
    }

    [Fact]
    public async Task Principal_carries_identity_role_and_stamp()
    {
        var user = await _app.Data.UserAsync(UserRole.Administrator, mustChangePassword: true);
        var principal = Auth.CreatePrincipal(user);
        Assert.Equal(user.Id.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.True(principal.IsInRole("Administrator"));
        Assert.Equal(user.SecurityStamp, principal.FindFirstValue(SecurityStamps.ClaimType));
        Assert.True(principal.HasClaim(SecurityStamps.MustChangePasswordClaim, "true"));
    }

    [Fact]
    public async Task Cookie_refresh_keeps_valid_sessions_updates_changed_ones_and_drops_stale_ones()
    {
        var user = await _app.Data.UserAsync();
        var principal = Auth.CreatePrincipal(user);
        Assert.Same(principal, await Auth.RefreshPrincipalAsync(principal));

        await _app.Db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.AcabAdmin));
        _app.NewScope();
        var refreshed = await Auth.RefreshPrincipalAsync(principal);
        Assert.NotSame(principal, refreshed);
        Assert.True(refreshed!.IsInRole("AcabAdmin"));

        await _app.Db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, "rotated"));
        _app.NewScope();
        Assert.Null(await Auth.RefreshPrincipalAsync(principal));

        await _app.Db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, user.SecurityStamp).SetProperty(u => u.IsBlocked, true));
        _app.NewScope();
        Assert.Null(await Auth.RefreshPrincipalAsync(principal));
        Assert.Null(await Auth.RefreshPrincipalAsync(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public async Task Confirmation_letters_are_throttled_per_address()
    {
        var user = await _app.Data.UserAsync(confirmed: false);
        await Auth.SendConfirmationAsync(user, user.Email, "https://cityfix.test/confirm?token=1");
        await Auth.SendConfirmationAsync(user, user.Email, "https://cityfix.test/confirm?token=2");

        var sent = Assert.Single(_app.Email.To(user.Email));
        Assert.Equal("https://cityfix.test/confirm?token=1", sent.Link);
        Assert.Contains(user.FirstName, sent.Message.Text);
    }

    [Fact]
    public async Task Account_exists_letter_contains_a_reset_link()
    {
        var user = await _app.Data.UserAsync();
        await Auth.SendAccountExistsAsync(user, "https://cityfix.test/reset?token=x");
        Assert.Equal("https://cityfix.test/reset?token=x", Assert.Single(_app.Email.To(user.Email)).Link);
    }

    [Fact]
    public async Task Residents_join_and_leave_associations_with_codes()
    {
        var resident = await _app.Data.UserAsync();
        var acab = await _app.Data.AcabAsync();

        var joined = await Auth.JoinAssociationAsync(resident.Id, acab.ResidentCode);
        Assert.Equal((null, acab.Name, UserRole.Resident), (joined.From, joined.To, joined.ToRole));
        await Assert.ThrowsAsync<ServiceValidationException>(() => Auth.JoinAssociationAsync(resident.Id, acab.ResidentCode));

        _app.NewScope();
        var left = await Auth.LeaveAssociationAsync(resident.Id);
        Assert.Null(left.To);
        Assert.Null((await ReloadAsync(resident.Id)).AcabId);
        var ex = await Assert.ThrowsAsync<ServiceValidationException>(() => Auth.LeaveAssociationAsync(resident.Id));
        Assert.Equal("Profile.Acab.None", ex.MessageKey);
    }

    [Fact]
    public async Task Head_stepping_down_stays_in_the_association_as_a_resident()
    {
        var acab = await _app.Data.AcabAsync();
        var head = await _app.Data.HeadAsync(acab);
        await Auth.LeaveAssociationAsync(head.Id);
        var reloaded = await ReloadAsync(head.Id);
        Assert.Equal((UserRole.Resident, acab.Id), (reloaded.Role, reloaded.AcabId));
    }

    [Fact]
    public async Task Administrators_cannot_use_association_codes()
    {
        var admin = await _app.Data.AdminAsync();
        var acab = await _app.Data.AcabAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => Auth.JoinAssociationAsync(admin.Id, acab.ResidentCode));
    }
}
