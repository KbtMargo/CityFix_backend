# EmptyState
Three empty patterns from the app.

- **EmptyState** (`ui/EmptyState.tsx`): a dashed `rounded-2xl` box (slate-300 / slate-700) with 40px padding. It holds a 56px inverse tile (`rounded-2xl`, slate-900, 24px icon), an 18px semibold title, a 14px slate-500 line (max 448px) and an optional default button.
- **Text box** (`--text`): the same dashed box with one slate-500 sentence, such as "Нічого не знайдено за поточними фільтрами."
- **Page box** (`--page`): a 2px dashed slate-300 `rounded-lg` box with 48px padding. Used for "Ви ще не створювали жодного звернення." and for notifications, with a 48px icon.

```cshtml
<div class="cf-empty"><div class="cf-empty__tile"><span class="cf-icon cf-icon--bars cf-icon--6"></span></div>
  <div class="cf-empty__title">Тут поки порожньо</div><div class="cf-empty__text">Змініть фільтри або створіть нове звернення.</div>
  <a class="cf-btn" asp-action="Create">Нове звернення</a></div>
```
