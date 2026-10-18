# ReportFilters
The search and filter row above every appeal list, and the filters popover (`patterns/ReportFilters.tsx`).

**Row.** `.cf-toolbar`: a full-width search input, then an outline **Фільтри** button with the blue count badge.

**Popover**
- `.cf-popover`: 320px wide, `rounded-md`, 1px `border`, `popover` fill, `shadow-md`, 16px padding.
- The title "Фільтри" (18px semibold) sits beside a round ghost close button.
- Fields: Дата (an outline button that opens a date range), Категорія and Установа (Select).
- The footer has Скинути (ghost) and Застосувати (default).

**Without JS.** Render the same fields as a GET form under the search row (`<details>` works well), and submit to the list action.

```cshtml
<form method="get" class="cf-toolbar">
  <div class="cf-toolbar__grow"><input class="cf-input" name="q" value="@Model.Q" placeholder="Пошук звернень…" aria-label="Пошук звернень" /></div>
  <details><summary class="cf-btn cf-btn--outline cf-btn--icon-gap"><span class="cf-icon cf-icon--sliders-horizontal cf-icon--4"></span>Фільтри
    @if (Model.ActiveFilters > 0) { <span class="cf-btn__count">@Model.ActiveFilters</span> }</summary>
    <div class="cf-popover">…fields…<div class="cf-popover__foot"><a class="cf-btn cf-btn--ghost" asp-action="Index">Скинути</a><button class="cf-btn">Застосувати</button></div></div>
  </details>
</form>
```
