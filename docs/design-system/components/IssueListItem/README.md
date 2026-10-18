# IssueListItem
One appeal in a list (`patterns/IssueListItem.tsx`). The whole Card is a link, and it contains:
- the title (18px semibold),
- the category Pill,
- the excerpt (`ink-secondary`),
- a meta row (pin, clock and users icons with address, date in `dd.MM.yyyy` and recipient),
- an optional 96 × 80 `rounded-xl` thumbnail with a hairline ring.

**Consumer provides.** Title, Category.Name, Description (show "Без опису" when it is empty), Address (or "Координати: lat, lng"), CreatedAt, Recipient.Name and the first file URL.

**Layout.** Stack items with `.cf-issue-list` (16px gap). Each opens ReportDetailModal (`?reportId=`).

**Don't**
- Truncate the title.
- Put buttons inside the item; the whole card is the link.

```cshtml
<div class="cf-issue-list">
@foreach (var r in Model) {
  <a class="cf-issue cf-card" asp-route-reportId="@r.Id">
    <div class="cf-issue__row"><div class="cf-issue__main">
      <h4 class="cf-issue__title">@r.Title</h4>
      <div class="cf-issue__tags"><span class="cf-pill cf-pill--neutral">@r.Category.Name</span></div>
      <p class="cf-issue__excerpt">@(r.Description ?? "Без опису")</p>
      <div class="cf-issue__meta"><span><span class="cf-icon cf-icon--pin cf-icon--4"></span>@(r.Address ?? $"Координати: {r.Lat}, {r.Lng}")</span>
        <span><span class="cf-icon cf-icon--clock cf-icon--4"></span>@r.CreatedAt.ToString("dd.MM.yyyy")</span>
        <span><span class="cf-icon cf-icon--users cf-icon--4"></span>@r.Recipient.Name</span></div></div>
      @if (r.Files.Any()) { <div class="cf-issue__thumb"><img src="@r.Files[0].Url" alt="Зображення для: @r.Title" /></div> }
    </div></a>
}
</div>
```
