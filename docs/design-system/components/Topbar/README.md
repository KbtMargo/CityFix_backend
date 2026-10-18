# Topbar
An optional white bar above the content (`components/layout/Topbar.tsx`), with a bottom border and the primary "Нове звернення" button on the right.

- It uses padding 12px 16px, and 12px 24px from 640px up. The border is `topbar-line` (slate-200 / slate-800).
- The button carries `cf-btn--icon-gap`, because the source adds `mr-2` to the plus icon.

**Do**
- Use it only on pages without the sidebar's "Нове звернення" item.

**Don't**
- Duplicate the sidebar links here.

```cshtml
<div class="cf-topbar"><div></div>
  <a class="cf-btn cf-btn--icon-gap" asp-controller="Issues" asp-action="Create"><span class="cf-icon cf-icon--plus cf-icon--4"></span>Нове звернення</a>
</div>
```
