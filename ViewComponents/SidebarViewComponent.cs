using System.Security.Claims;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CityFix_backend.ViewComponents
{
    public record NavItem(string Controller, string Action, string Icon, string LabelKey, string SubKey, int? Count, bool Active);

    public record SidebarModel(AppealStats Stats, IReadOnlyList<NavItem> Items);

    // The left navigation: logo, Open/Closed stats, the role's menu, language switch, help box.
    public class SidebarViewComponent : ViewComponent
    {
        private readonly IAppealService _appeals;
        private readonly INotificationService _notifications;

        public SidebarViewComponent(IAppealService appeals, INotificationService notifications)
        {
            _appeals = appeals;
            _notifications = notifications;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = int.Parse(UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _appeals.GetUserAsync(userId);
            var stats = await _appeals.GetStatsAsync(userId);

            var controller = ViewContext.RouteData.Values["controller"]?.ToString() ?? "";
            var action = ViewContext.RouteData.Values["action"]?.ToString() ?? "";
            bool Is(string c) => string.Equals(controller, c, StringComparison.OrdinalIgnoreCase);
            var creating = Is("Appeals") && string.Equals(action, "Create", StringComparison.OrdinalIgnoreCase);

            var items = new List<NavItem>();
            if (user.Role == UserRole.Resident)
            {
                var unread = await _notifications.CountUnreadAsync(userId);
                items.Add(new("Dashboard", "Index", "users", "Nav.Home", "Nav.Home.Sub", null, Is("Dashboard")));
                items.Add(new("Appeals", "Index", "list", "Nav.MyAppeals", "Nav.MyAppeals.Sub", null, Is("Appeals") && !creating));
                items.Add(new("Appeals", "Create", "plus", "Nav.NewAppeal", "Nav.NewAppeal.Sub", null, creating));
                items.Add(new("Analytics", "Index", "bars", "Nav.Analytics", "Nav.Analytics.Sub", null, Is("Analytics")));
                items.Add(new("Notifications", "Index", "bell", "Nav.Notifications", "Nav.Notifications.Sub", unread > 0 ? unread : null, Is("Notifications")));
            }
            else
            {
                items.Add(new("Dashboard", "Index", "home", "Nav.Home", "Nav.Home.SubAdmin", null, Is("Dashboard")));
                items.Add(new("Analytics", "Index", "bars", "Nav.Analytics", "Nav.Analytics.Sub", null, Is("Analytics")));
                items.Add(new("Appeals", "Index", "list", "Nav.AllAppeals", "Nav.AllAppeals.Sub", null, Is("Appeals")));
                items.Add(new("Users", "Index", "users", "Nav.Users", "Nav.Users.Sub", null, Is("Users")));
                if (user.Role == UserRole.Administrator)
                {
                    items.Add(new("Acabs", "Index", "apartment", "Nav.Acabs", "Nav.Acabs.Sub", null, Is("Acabs")));
                    items.Add(new("Categories", "Index", "list-lucide", "Nav.Categories", "Nav.Categories.Sub", null, Is("Categories")));
                    items.Add(new("Recipients", "Index", "arrow-right", "Nav.Recipients", "Nav.Recipients.Sub", null, Is("Recipients")));
                }
            }
            items.Add(new("Account", "Profile", "shield", "Nav.Profile", "Nav.Profile.Sub", null, Is("Account")));

            return View(new SidebarModel(stats, items));
        }
    }
}
