# Dialog
There are two kinds of modal in the app.

**shadcn Dialog** (`ui/dialog.tsx`, used for analytics export)
- A 50% black backdrop.
- Content: `background` fill, 1px `border`, `rounded-lg`, `shadow-lg`, 24px padding, 16px gap, up to 512px wide.
- The title is 18px semibold `leading-none`; the description is 14px `muted-foreground`.
- The footer is right-aligned on desktop and stacked in reverse on mobile.
- The close × sits top right at 70% opacity.

**Admin panel modal** (edit and delete user)
- `.cf-panel-modal`: `surface-raised`, `rounded-lg`, `shadow-xl`, 24px padding, with a 20px semibold title.
- The delete version has a red-600 title with an alert icon, and the red **Alert** box when deletion is blocked.

```cshtml
<div class="cf-overlay cf-overlay--dialog"><div class="cf-panel-modal cf-panel-modal--sm" role="dialog" aria-labelledby="del">
  <h2 class="cf-panel-modal__title cf-panel-modal__title--danger" id="del"><span class="cf-icon cf-icon--alert cf-icon--5"></span>Видалити користувача?</h2>
  <p class="cf-panel-modal__text">Цю дію не можна скасувати.</p>
  <form asp-action="Delete" method="post" class="cf-panel-modal__foot"><a class="cf-btn cf-btn--outline" asp-action="Index">Закрити</a><button class="cf-btn cf-btn--destructive">Видалити</button></form>
</div></div>
```
