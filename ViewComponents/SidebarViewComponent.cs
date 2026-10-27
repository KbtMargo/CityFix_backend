using System.Security.Claims;
using CityFix_backend.Helpers;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CityFix_backend.ViewComponents
{
    public record NavItem(string Controller, string Action, string Icon, string LabelKey, string SubKey, int? Count, bool Active);

    public record SidebarModel(AppealStats Stats, IReadOnlyList<NavItem> Items, string UserName, UserRole Role, bool ProfileActive)
    {
        public string Initials => string.Concat(UserName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));
    }

    public class SidebarViewComponent : ViewComponent
    {
        private readonly IUserService _users;
        private readonly IAppealService _appeals;
        private readonly INotificationService _notifications;

        public SidebarViewComponent(IUserService users, IAppealService appeals, INotificationService notifications)
        {
            _users = users;
            _appeals = appeals;
            _notifications = notifications;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _users.GetActorAsync(int.Parse(UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)!));
            var stats = await _appeals.GetStatsAsync(user);

            var controller = ViewContext.RouteData.Values["controller"]?.ToString() ?? "";
            var action = ViewContext.RouteData.Values["action"]?.ToString() ?? "";
            bool Is(string c) => string.Equals(controller, c, StringComparison.OrdinalIgnoreCase);
            var creating = Is("Appeals") && string.Equals(action, "Create", StringComparison.OrdinalIgnoreCase);

            int? unread = null;
            if (user.Role != UserRole.Administrator)
            {
                var count = await _notifications.CountUnreadAsync(user.Id);
                unread = count > 0 ? count : null;
            }

            var items = new List<NavItem>();
            if (user.Role == UserRole.Resident)
            {
                items.Add(new("Dashboard", "Index", "home", "Nav.Home", "Nav.Home.Sub", null, Is("Dashboard")));
                items.Add(new("Appeals", "Index", "list", "Nav.MyAppeals", "Nav.MyAppeals.Sub", null, Is("Appeals") && !creating));
                items.Add(new("Appeals", "Create", "plus", "Nav.NewAppeal", "Nav.NewAppeal.Sub", null, creating));
                items.Add(new("Analytics", "Index", "bars", "Nav.Analytics", "Nav.Analytics.Sub", null, Is("Analytics")));
                items.Add(new("Notifications", "Index", "bell", "Nav.Notifications", "Nav.Notifications.Sub", unread, Is("Notifications")));
            }
            else
            {
                items.Add(new("Dashboard", "Index", "home", "Nav.Home", "Nav.Home.SubAdmin", null, Is("Dashboard")));
                items.Add(new("Analytics", "Index", "bars", "Nav.Analytics", "Nav.Analytics.Sub", null, Is("Analytics")));
                items.Add(new("Appeals", "Index", "list", "Nav.AllAppeals", "Nav.AllAppeals.Sub", null, Is("Appeals")));
                if (user.Role == UserRole.AcabAdmin)
                {
                    items.Add(new("Notifications", "Index", "bell", "Nav.Notifications", "Nav.Notifications.Sub", unread, Is("Notifications")));
                }
                items.Add(new("Users", "Index", "users", "Nav.Users", "Nav.Users.Sub", null, Is("Users")));
                if (user.Role == UserRole.Administrator)
                {
                    items.Add(new("Acabs", "Index", "apartment", "Nav.Acabs", "Nav.Acabs.Sub", null, Is("Acabs")));
                    items.Add(new("Categories", "Index", "tag", "Nav.Categories", "Nav.Categories.Sub", null, Is("Categories")));
                    items.Add(new("Recipients", "Index", "wrench", "Nav.Recipients", "Nav.Recipients.Sub", null, Is("Recipients")));
                    items.Add(new("Audit", "Index", "shield", "Nav.Audit", "Nav.Audit.Sub", null, Is("Audit")));
                }
            }

            return View(new SidebarModel(stats, items, user.FullName.Length > 0 ? user.DisplayName() : user.Email, user.Role, Is("Account")));
        }
    }
}
