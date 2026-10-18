# Tabs
There are two kinds of tabs in the app.

**Segmented** (`ui/Tabs.tsx`)
- A white `rounded-xl` strip with a slate-200 ring and 4px padding.
- Each tab is `rounded-lg`, padding 12px × 6px, 14px medium slate-600.
- The active tab is inverse (slate-900 / white).
- Use it for list filters: Усі, Нові, В роботі, Закриті.

**Underline** (the notifications page)
- 14px medium tabs with a 2px bottom rule over a slate-200 divider.
- The active tab is slate-900 (slate-50 in dark).
- An optional `__count` chip.

```cshtml
<div class="cf-tabs" role="tablist">
  <a class="cf-tab" role="tab" aria-selected="@(tab == "all" ? "true" : "false")" asp-route-tab="all">Усі</a>
  <a class="cf-tab" role="tab" aria-selected="@(tab == "new" ? "true" : "false")" asp-route-tab="new">Нові</a>
</div>
<div class="cf-utabs"><nav class="cf-utabs__nav" role="tablist">
  <a class="cf-utab" role="tab" aria-selected="true">Непрочитані<span class="cf-utab__count">@unread</span></a><a class="cf-utab" role="tab" aria-selected="false">Всі</a></nav></div>
```
