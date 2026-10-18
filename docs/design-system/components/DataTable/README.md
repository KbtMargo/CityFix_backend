# DataTable
The admin lists: Користувачі, ОСББ and Всі звернення (`admin/users/page.tsx`).

**Page head.** A "← Назад до адмін-панелі" link (14px medium slate-600). Then the title (30px bold) on the left and "Всього користувачів: **128**" on the right.

**Table**
- `.cf-table-card`: white (slate-800), `rounded-lg`, `shadow`.
- The header row is 12px uppercase bold, slate-700 text on slate-50 (slate-400 on slate-700 in dark). Cells are padded 24px × 12px.
- Body rows have 24px × 16px cells, a bottom `border`, and slate-50 on hover.
- The first column is the name (semibold) with the phone below in slate-500.

**Tags.** `.cf-tag` is a rounded-full, 12px medium label with no ring.
- Roles: `--admin` red-100, `--head` blue-100, `--resident` green-100.
- Count: `--count` orange-100, or `--zero` slate-100 when it is 0.

**Actions.** `.cf-icon-action`: a 16px lucide icon in an 8px-padded `rounded-lg` button.
- `--edit` is blue-500 with a blue-50 hover.
- `--delete` is red-500. Disable it when the user has appeals.
- Always give these buttons an `aria-label`. The source uses only `title`.

```cshtml
<div class="cf-table-card"><div class="cf-table-scroll"><table class="cf-table">
  <thead><tr><th scope="col">Користувач</th><th scope="col">Email</th><th scope="col">Роль</th><th scope="col">ОСББ</th><th scope="col">Звернень</th><th scope="col">Дата реєстрації</th><th scope="col" class="cf-center">Дії</th></tr></thead>
  <tbody>@foreach (var u in Model.Users) {
    <tr><td class="cf-strong"><div class="cf-table__name">@u.FirstName @u.LastName</div>@if (u.Phone != null) { <div class="cf-table__sub">@u.Phone</div> }</td>
      <td>@u.Email</td><td><span class="cf-tag cf-tag--@u.RoleKey">@u.RoleLabel</span></td><td>@(u.Osbb?.Name ?? "Не вказано")</td>
      <td><span class="cf-tag @(u.ReportsCount > 0 ? "cf-tag--count" : "cf-tag--zero")">@u.ReportsCount</span></td><td>@u.CreatedAt.ToString("dd.MM.yyyy")</td>
      <td><div class="cf-row-actions"><a class="cf-icon-action cf-icon-action--edit" asp-action="Edit" asp-route-id="@u.Id" aria-label="Редагувати"><span class="cf-icon cf-icon--edit cf-icon--4"></span></a>
        <a class="cf-icon-action cf-icon-action--delete" asp-action="Delete" asp-route-id="@u.Id" aria-label="Видалити"><span class="cf-icon cf-icon--trash cf-icon--4"></span></a></div></td></tr>
  }</tbody></table></div>
  <div class="cf-pagination-bar"><span>Показано @Model.From–@Model.To з @Model.Total</span><partial name="_Pagination" model="Model.Page" /></div>
</div>
```
