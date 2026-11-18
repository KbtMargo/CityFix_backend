using System.Reflection;
using CityFix_backend.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CityFix_backend.Tests.Architecture;

[Trait("Category", "Architecture")]
public class ArchitectureRulesTests
{
    private static readonly Assembly App = typeof(AppController).Assembly;

    private static readonly Type[] Controllers = App.GetTypes()
        .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
        .ToArray();

    private static IEnumerable<MethodInfo> Actions(Type controller) => controller
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(Controller) && m.DeclaringType != typeof(ControllerBase) && m.DeclaringType != typeof(object)
            && m.GetCustomAttribute<NonActionAttribute>() == null);

    private static IEnumerable<MethodInfo> PostActions => Controllers.SelectMany(Actions).Where(a => a.GetCustomAttribute<HttpPostAttribute>() != null);

    private static string Name(MethodInfo m) => $"{m.ReflectedType!.Name}.{m.Name}";

    [Fact]
    public void Every_post_action_validates_the_antiforgery_token()
    {
        var unprotected = PostActions
            .Where(a => a.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() == null
                && a.ReflectedType!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>(inherit: true) == null
                && a.ReflectedType!.GetCustomAttribute<AutoValidateAntiforgeryTokenAttribute>(inherit: true) == null)
            .Select(Name).ToList();
        Assert.Empty(unprotected);
        Assert.True(PostActions.Count() > 30);
    }

    [Fact]
    public void Only_public_pages_live_in_controllers_without_authorize()
    {
        var open = Controllers.Where(c => c.GetCustomAttribute<AuthorizeAttribute>(inherit: true) == null).Select(c => c.Name).Order();
        Assert.Equal(["AccountController", "CultureController", "HomeController"], open);
    }

    [Theory]
    [InlineData(typeof(AcabsController))]
    [InlineData(typeof(CategoriesController))]
    [InlineData(typeof(RecipientsController))]
    [InlineData(typeof(AuditController))]
    public void Reference_data_and_audit_are_for_administrators_only(Type controller)
    {
        Assert.Equal("Administrator", controller.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.DoesNotContain(Actions(controller), a => a.GetCustomAttribute<AllowAnonymousAttribute>() != null);
    }

    [Fact]
    public void User_management_writes_are_for_administrators_only()
    {
        var writes = Actions(typeof(UsersController)).Where(a => a.GetCustomAttribute<HttpPostAttribute>() != null || a.Name is "Create" or "Edit" or "Delete");
        Assert.All(writes, a => Assert.Equal("Administrator", a.GetCustomAttribute<AuthorizeAttribute>()?.Roles));
    }

    [Fact]
    public void Account_actions_that_change_an_account_require_sign_in()
    {
        var anonymousPosts = new[] { "Register", "ResendConfirmation", "ForgotPassword", "ResetPassword", "Login" };
        var unguarded = Actions(typeof(AccountController))
            .Where(a => a.GetCustomAttribute<HttpPostAttribute>() != null && !anonymousPosts.Contains(a.Name))
            .Where(a => a.GetCustomAttribute<AuthorizeAttribute>() == null)
            .Select(Name).ToList();
        Assert.Empty(unguarded);
        Assert.NotNull(typeof(AccountController).GetMethod(nameof(AccountController.ExportData))!.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Theory]
    [InlineData("Login")]
    [InlineData("Register")]
    [InlineData("ForgotPassword")]
    [InlineData("ResetPassword")]
    [InlineData("ResendConfirmation")]
    [InlineData("ChangePassword")]
    [InlineData("DeleteAccount")]
    [InlineData("JoinAssociation")]
    public void Password_and_code_guessing_endpoints_are_rate_limited(string action)
    {
        var post = Actions(typeof(AccountController)).Single(a => a.Name == action && a.GetCustomAttribute<HttpPostAttribute>() != null);
        Assert.Equal("auth", post.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
    }

    [Fact]
    public void Lookup_endpoints_are_rate_limited()
    {
        Assert.Equal("lookup", typeof(GeoController).GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
        Assert.Equal("lookup", typeof(AppealsController).GetMethod(nameof(AppealsController.Similar))!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
        Assert.Equal("lookup", typeof(AppealsController).GetMethod(nameof(AppealsController.Support))!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
    }

    [Fact]
    public void Appeal_forms_bind_only_whitelisted_fields()
    {
        var posts = Actions(typeof(AppealsController)).Where(a => a.Name is "Create" or "Edit" && a.GetCustomAttribute<HttpPostAttribute>() != null);
        Assert.All(posts, a => Assert.Contains(a.GetParameters(), p => p.GetCustomAttribute<BindAttribute>() != null));
    }

    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (var field in type.GetFields(all)) yield return field.FieldType;
        foreach (var ctor in type.GetConstructors(all)) foreach (var p in ctor.GetParameters()) yield return p.ParameterType;
        foreach (var method in type.GetMethods(all))
        {
            yield return method.ReturnType;
            foreach (var p in method.GetParameters()) yield return p.ParameterType;
        }
    }

    [Fact]
    public void Services_and_models_do_not_depend_on_the_web_layer()
    {
        var lower = App.GetTypes().Where(t => t.Namespace is "CityFix_backend.Services" or "CityFix_backend.Services.Interfaces" or "CityFix_backend.Models" or "CityFix_backend.Data");
        var violations = lower
            .SelectMany(t => ReferencedTypes(t).Select(r => (t, r)))
            .Where(x => x.r.Namespace is "CityFix_backend.Controllers" or "CityFix_backend.ViewModels" or "CityFix_backend.ViewComponents")
            .Select(x => $"{x.t.Name} -> {x.r.Name}")
            .Distinct().ToList();
        Assert.Empty(violations);
    }

    [Fact]
    public void Controllers_use_service_interfaces_not_implementations()
    {
        var concrete = Controllers
            .SelectMany(c => c.GetConstructors().SelectMany(ctor => ctor.GetParameters()).Select(p => (c, p.ParameterType)))
            .Where(x => x.ParameterType.Namespace == "CityFix_backend.Services" && x.ParameterType.IsClass && x.ParameterType != typeof(Services.AuditLog))
            .Select(x => $"{x.c.Name}({x.ParameterType.Name})").ToList();
        Assert.Empty(concrete);
    }

    [Fact]
    public void Every_service_interface_has_exactly_one_implementation()
    {
        var interfaces = App.GetTypes().Where(t => t.IsInterface && t.Namespace == "CityFix_backend.Services.Interfaces");
        foreach (var contract in interfaces)
        {
            var implementations = App.GetTypes().Where(t => t.IsClass && !t.IsAbstract && contract.IsAssignableFrom(t)).ToList();
            Assert.True(implementations.Count >= 1, $"{contract.Name} has no implementation");
            if (contract.Name is not ("ITranslator" or "IEmailSender")) Assert.Single(implementations);
        }
    }

    [Fact]
    public void Entities_with_times_store_them_as_utc_datetimes()
    {
        var models = App.GetTypes().Where(t => t.Namespace == "CityFix_backend.Models" && t.IsClass);
        var offsets = models.SelectMany(t => t.GetProperties()).Where(p => p.PropertyType == typeof(DateTimeOffset) || p.PropertyType == typeof(DateTimeOffset?));
        Assert.Empty(offsets);
    }
}
