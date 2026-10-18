# Notifications
The `dashboard/notifications` page, in an 896px column with 24px padding.

**Header.** "Сповіщення" (24px semibold) and a slate-500 subtitle on the left. On the right, a plain bordered button "Позначити всі як прочитані".

**Tabs.** Underline tabs: Непрочитані (with a count chip) and Всі.

**Rows**
- `.cf-note`: 16px padding, 1px `border`, a 4px left border, `rounded-lg`.
- A bell icon (20px slate-500), the title (medium), the relative time on the right ("Щойно", "2 год тому", "3 д тому", then a date), and the message (14px, line-height 1.625).
- Unread (`is-unread`): blue-50/50 fill, a gray-600 left rule, slate-800 title and slate-700 text.
- Read: muted slate-500.
- Hover adds `shadow-md`.
- Clicking a row opens the notification and marks it read.

**Empty.** A dashed 2px slate-300 box with a 48px bell, "Все переглянуто!" and "Нових сповіщень немає."

```cshtml
<div class="cf-notes">
@foreach (var n in Model.Items) {
  <a class="cf-note @(n.IsRead ? "" : "is-unread")" asp-action="Open" asp-route-id="@n.Id">
    <span class="cf-icon cf-icon--bell-lucide cf-icon--5 cf-note__icon"></span>
    <div class="cf-note__main"><div class="cf-note__top"><h4 class="cf-note__title">@n.Title</h4><span class="cf-note__time">@Html.TimeAgo(n.CreatedAt)</span></div>
      <p class="cf-note__text">@n.Message</p></div></a>
}
</div>
```
