# Sidebar
The left navigation (`components/layout/AppSidebar.tsx`), from top to bottom: the logo, a stats box (Відкрито / Закрито), role-specific menu items with an icon tile, a title and a sub-line, and a help box pinned to the bottom.

**Layout**
- Resident pages: `.cf-shell` gives a 320px sidebar (white, right border) and a scrolling main area on `page`.
- Admin and OSBB pages: `.cf-shell--admin` gives a 280px column with no border and a `page-admin` (slate-100) main area with 32px padding. Under 768px the sidebar is hidden.

**Menus**
- RESIDENT: Головна, Мої звернення, Нове звернення, Аналітика, Сповіщення, Профіль.
- ADMIN: Головна, Аналітика, Всі звернення, Користувачі, ОСББ, Профіль.
- OSBB_ADMIN: Головна, Аналітика, Всі звернення, Користувачі, Профіль.

**Active item.** Set `aria-current="page"` or `.is-active`. The row turns slate-100 and the icon tile turns inverse (slate-900, or white in dark). Match on the path prefix, as the source does.

**Counts.** `.cf-count` holds the unread count on Сповіщення. Use `--highlight` (emerald) for the highlighted item.

**Do**
- Keep each item's sub-line short: one line, 12px.
- Fill the stats box from `/api/users/stats`. While it loads, show "Завантаження...".

**Don't**
- Add a top navigation bar for the same links.
- Put more than one count badge on an item.

```cshtml
<div class="cf-shell">
  <aside class="cf-shell__sidebar"><div class="cf-sidebar">
    <div class="cf-sidebar__head"><partial name="_Logo" /></div>
    <div class="cf-sidebar__body">
      <div class="cf-box cf-stats-box">
        <div class="cf-stats-box__row"><span class="cf-stats-box__label"><span class="cf-dot cf-dot--open"></span>Відкрито</span><span class="cf-stats-box__value">@stats.Open</span></div>
        <div class="cf-stats-box__row"><span class="cf-stats-box__label"><span class="cf-dot cf-dot--closed"></span>Закрито</span><span class="cf-stats-box__value">@stats.Closed</span></div>
      </div>
      <nav class="cf-sidebar__nav" aria-label="Головне меню">
        @foreach (var item in Model.NavItems) {
          <a class="cf-nav-item" href="@item.Href" aria-current="@(item.Active ? "page" : null)">
            <span class="cf-nav-item__main"><span class="cf-nav-item__icon"><span class="cf-icon cf-icon--@item.Icon"></span></span>
              <span class="cf-nav-item__text"><span class="cf-nav-item__label">@item.Label</span><span class="cf-nav-item__sub">@item.Sub</span></span></span>
            @if (item.Count is int n) { <span class="cf-count">@n</span> }
          </a>
        }
      </nav>
    </div>
    <div class="cf-sidebar__foot"><div class="cf-box cf-help-box"><div class="cf-help-box__title">Допомога та підтримка</div><div class="cf-help-box__text">Питання? Напишіть у службу підтримки.</div></div></div>
  </div></aside>
  <main class="cf-shell__main">@RenderBody()</main>
</div>
```
