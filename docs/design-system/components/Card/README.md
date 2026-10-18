# Card
The base surface from `components/ui/Card.tsx`: `rounded-2xl`, `surface` (white / slate-900) and no shadow at rest. On hover it lifts with a tinted `shadow-md` (`shadow-card-hover`).

**Parts**
- `.cf-card__head` (padding 20px × 16px) holds `__title` (semibold) and `__desc` (14px slate-500).
- `.cf-card__body` has 20px padding.
- `.cf-card--p6` gives 24px padding for chart cards, with `.cf-card__h` as the heading.
- **Stat** (`ui/Stat.tsx`): `.cf-stat` with a 24px semibold value and a 14px label.

**Do**
- Put cards on `page` (slate-50).

**Don't**
- Add borders to cards; the source cards are flat.

```cshtml
<div class="cf-card"><div class="cf-card__head"><div class="cf-card__title">Останні звернення</div><div class="cf-card__desc">Оновлено 5 хвилин тому</div></div>
  <div class="cf-card__body">…</div></div>
```
