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
        private readonly IStringLocalizer<SharedResource> _l;

        public NotificationsController(INotificationService notifications, IStringLocalizer<SharedResource> localizer)
        {
            _notifications = notifications;
            _l = localizer;
        }

        // GET: Notifications?tab=unread|all
        public async Task<IActionResult> Index(string? tab)
        {
            var unreadTab = tab != "all";
            return View(new NotificationsViewModel
            {
                Items = await _notifications.GetForUserAsync(CurrentUserId, unreadTab),
                UnreadCount = await _notifications.CountUnreadAsync(CurrentUserId),
                UnreadTab = unreadTab
            });
        }

        // POST: Notifications/MarkRead/5  (clicking a row: mark it read and open its appeal)
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

        // POST: Notifications/MarkAllRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            await _notifications.MarkAllReadAsync(CurrentUserId);
            FlashSuccess(_l["Notifications.AllRead"]);
            return RedirectToAction(nameof(Index));
        }
    }
}
