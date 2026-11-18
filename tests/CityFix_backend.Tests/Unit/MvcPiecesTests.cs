using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CityFix_backend.Controllers;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace CityFix_backend.Tests.Unit;

[Trait("Category", "Unit")]
public class ViewModelValidationTests
{
    private static List<string> Errors(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results.SelectMany(r => r.MemberNames).ToList();
    }

    private static RegisterViewModel ValidRegistration() => new()
    {
        FirstName = "Олена", LastName = "Коваль", Email = "olena@example.com", Password = "12345678", ConfirmPassword = "12345678"
    };

    [Fact]
    public void Valid_registration_passes() => Assert.Empty(Errors(ValidRegistration()));

    [Fact]
    public void Registration_requires_a_long_enough_matching_password_and_a_real_email()
    {
        var model = ValidRegistration();
        model.Password = "1234567";
        model.ConfirmPassword = "different";
        model.Email = "not-an-email";
        model.Phone = "call me maybe";
        Assert.Equal(["Email", "Phone", "Password", "ConfirmPassword"], Errors(model));
    }

    [Theory]
    [InlineData("Яма", false)]
    [InlineData("Ямааа", true)]
    public void Appeal_title_needs_at_least_five_characters(string title, bool valid)
    {
        var form = new AppealFormViewModel { Title = title, CategoryId = 1, Responsible = "acab" };
        Assert.Equal(valid, !Errors(form).Contains("Title"));
    }

    [Fact]
    public void Appeal_coordinates_must_be_on_the_globe_and_category_and_responsible_are_required()
    {
        var form = new AppealFormViewModel { Title = "Яма на дорозі", Lat = 91, Lng = -181 };
        Assert.Equal(["Lat", "Lng", "CategoryId", "Responsible"], Errors(form));
    }

    [Theory]
    [InlineData("acab", ResponsibleKind.Association, null)]
    [InlineData("12", ResponsibleKind.Recipient, 12)]
    [InlineData("junk", ResponsibleKind.Recipient, null)]
    public void Appeal_form_maps_the_responsible_choice(string responsible, ResponsibleKind kind, int? recipientId)
    {
        var input = new AppealFormViewModel { Title = "Яма", Responsible = responsible, CategoryId = null }.ToInput();
        Assert.Equal((kind, recipientId, 0), (input.Responsible, input.RecipientId, input.CategoryId));
    }

    [Fact]
    public void Appeal_form_round_trips_an_appeal()
    {
        var appeal = new Appeal { Id = 3, Title = "Яма", Lat = 1.5m, Lng = 2.5m, CategoryId = 4, ResponsibleKind = ResponsibleKind.Recipient, RecipientId = 9 };
        var form = AppealFormViewModel.From(appeal);
        Assert.Equal((3, "9", 4), (form.Id, form.Responsible, form.CategoryId));
        Assert.Equal("acab", AppealFormViewModel.ResponsibleValue(new Appeal { ResponsibleKind = ResponsibleKind.Association }));
    }

    [Fact]
    public void Password_change_requires_confirmation()
    {
        Assert.Equal(["ConfirmPassword"], Errors(new ChangePasswordViewModel { CurrentPassword = "x", NewPassword = "longenough", ConfirmPassword = "other" }));
        Assert.Equal(["NewPassword", "ConfirmPassword"], Errors(new ResetPasswordViewModel { Token = "t", NewPassword = "short", ConfirmPassword = "" }));
    }
}

[Trait("Category", "Unit")]
public class MvcFilterTests
{
    private static ActionContext Context(ClaimsPrincipal? user = null, string controller = "Appeals", string action = "Index") => new(
        new DefaultHttpContext { User = user ?? new ClaimsPrincipal(new ClaimsIdentity()) },
        new RouteData(),
        new ControllerActionDescriptor { ControllerName = controller, ActionName = action });

    private static ClaimsPrincipal SignedIn(bool mustChangePassword = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "1") };
        if (mustChangePassword) claims.Add(new Claim(SecurityStamps.MustChangePasswordClaim, "true"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
    }

    private static IActionResult? Handle(Exception exception, ClaimsPrincipal? user = null)
    {
        var context = new ExceptionContext(Context(user), []) { Exception = exception };
        new ServiceExceptionFilter().OnException(context);
        Assert.Equal(context.Result != null, context.ExceptionHandled);
        return context.Result;
    }

    [Fact]
    public void Missing_records_become_404() => Assert.IsType<NotFoundResult>(Handle(new NotFoundException("x")));

    [Fact]
    public void Forbidden_actions_become_403_for_signed_in_users_and_a_login_for_guests()
    {
        Assert.IsType<ForbidResult>(Handle(new ForbiddenException("x"), SignedIn()));
        Assert.IsType<ChallengeResult>(Handle(new ForbiddenException("x")));
    }

    [Fact]
    public void Other_exceptions_are_left_to_the_error_page() => Assert.Null(Handle(new InvalidOperationException()));

    private sealed class Localizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class TempDataFactory : ITempDataDictionaryFactory
    {
        public Dictionary<string, object?> Values { get; } = new();
        public ITempDataDictionary GetTempData(HttpContext context) => new TempData(Values);

        private sealed class TempData(Dictionary<string, object?> values) : Dictionary<string, object?>, ITempDataDictionary
        {
            public new object? this[string key] { get => values[key]; set => values[key] = value; }
            public void Keep() { }
            public void Keep(string key) { }
            public void Load() { }
            public object? Peek(string key) => values.GetValueOrDefault(key);
            public void Save() { }
        }
    }

    private static (IActionResult? Result, TempDataFactory TempData) Execute(ClaimsPrincipal user, string controller, string action)
    {
        var tempData = new TempDataFactory();
        var context = new ActionExecutingContext(Context(user, controller, action), [], new Dictionary<string, object?>(), new object());
        new PasswordChangeRequiredFilter(new Localizer(), tempData).OnActionExecuting(context);
        return (context.Result, tempData);
    }

    [Fact]
    public void Users_with_a_temporary_password_are_sent_to_the_profile()
    {
        var (result, tempData) = Execute(SignedIn(mustChangePassword: true), "Appeals", "Index");
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(("Account", "Profile"), (redirect.ControllerName, redirect.ActionName));
        Assert.Equal("Auth.MustChangePassword", tempData.Values[AppController.ErrorKey]);
    }

    [Theory]
    [InlineData("Account", "ChangePassword")]
    [InlineData("Account", "Logout")]
    [InlineData("Account", "Profile")]
    [InlineData("Culture", "Set")]
    [InlineData("Home", "Status")]
    public void Password_change_pages_stay_reachable(string controller, string action)
    {
        Assert.Null(Execute(SignedIn(mustChangePassword: true), controller, action).Result);
    }

    [Fact]
    public void Normal_users_are_not_redirected()
    {
        Assert.Null(Execute(SignedIn(), "Appeals", "Index").Result);
    }
}
