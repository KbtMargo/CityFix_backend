# Pagination
**Addition.** The frontend has no pagination yet, so this control is built only from its own parts: outline `sm` Buttons with lucide chevrons, and a 14px slate-500 status, "Сторінка **3** з 7".

- **Where.** Put it in `.cf-pagination-bar` at the bottom of a `cf-table-card` (top border, padding 24px × 12px), with "Показано 41–60 з 128" on the left.
- **Disabled ends.** At the first or last page, render a `<span aria-disabled="true">` with the same classes. It shows at 50% opacity, the shadcn disabled look.
- **Links.** Use `?page=N` links, keeping the current filters.

```cshtml
<nav class="cf-pagination" aria-label="Сторінки">
  @if (Model.Page > 1) { <a class="cf-btn cf-btn--outline cf-btn--sm" rel="prev" asp-all-route-data="Model.PrevRoute"><span class="cf-icon cf-icon--chevron-left cf-icon--4"></span>Назад</a> }
  else { <span class="cf-btn cf-btn--outline cf-btn--sm" aria-disabled="true"><span class="cf-icon cf-icon--chevron-left cf-icon--4"></span>Назад</span> }
  <span class="cf-pagination__status" aria-current="page">Сторінка <b>@Model.Page</b> з @Model.TotalPages</span>
  @* Далі — mirror *@
</nav>
```
