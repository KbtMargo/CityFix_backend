using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
    [Authorize]
    public class NotificationsController : AppController
    {
        private readonly INotificationService _notifications;

        public NotificationsController(INotificationService notifications, IUserService users, IStringLocalizer<SharedResource> localizer)
            : base(users, localizer)
        {
            _notifications = notifications;
        }

        public async Task<IActionResult> Index(string? tab, int page = 1)
        {
            var unreadTab = tab != "all";
            return View(new NotificationsViewModel
            {
                Page = await _notifications.GetForUserAsync(CurrentUserId, unreadTab, page),
                UnreadCount = await _notifications.CountUnreadAsync(CurrentUserId),
                UnreadTab = unreadTab
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            var notification = await _notifications.MarkReadAsync(id, CurrentUserId);
            if (notification == null)
            {
                return NotFound();
            }
            return notification.AppealId != null
                ? RedirectToAction("Index", "Appeals", new { appealId = notification.AppealId })
                : RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            await _notifications.MarkAllReadAsync(CurrentUserId);
            FlashSuccess(L["Notifications.AllRead"]);
            return RedirectToAction(nameof(Index));
        }
    }
}
