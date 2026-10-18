# FormField
The app's form controls: **Input** and **TextArea** (`ui/Input.tsx`, `ui/TextArea.tsx`), the shadcn **Label**, **Select** (`ui/Select.tsx`), plus **Checkbox** and **Switch**.

**Input / TextArea**
- `rounded-xl` (14px), 1px `line-strong` border (slate-300), white or slate-900 fill, padding 12px × 8px, 14px text.
- On focus a 2px slate-300 ring appears.
- Wrap in `label.cf-field`. The label above is `__label` (14px medium, slate-700 / slate-200).
- Below go the hint (`__hint`, 12px slate-500) or the error (`__error`, 12px red-600).
- An error sets the border to red-500 (`--error`, or ASP.NET's `input-validation-error`).

**Label (shadcn).** `.cf-label` is 14px medium, `leading-none`, and **inline**, exactly like the Radix label. It is what the new-issue form uses above each control.

**Select**
- Trigger: 40px, `rounded-md` (8px), slate-200 border, white or slate-950 fill, with a chevron at 50% opacity.
- In Razor, wrap a native `<select>` in `div.cf-select`.
- The open list (`.cf-listbox` / `.cf-option`) is shown for reference. Native selects use the browser's list.

**Checkbox and Switch**
- Checkbox: the native control at 16px, as in the app (no forms plugin).
- Switch: a 44 × 24 track (slate-300, slate-900 when on) with a white 20px thumb.

**Contrast notes, kept from the source**
- Placeholder slate-400 on white is 2.6:1.
- The input border slate-300 on white is 1.5:1.

```cshtml
<div><label class="cf-label" asp-for="Title">Заголовок</label>
  <label class="cf-field"><input asp-for="Title" class="cf-input" placeholder="Коротко опишіть проблему" /></label>
  <span asp-validation-for="Title"></span></div>
<div><label class="cf-label" asp-for="CategoryId">Категорія</label>
  <div class="cf-select"><select asp-for="CategoryId" asp-items="ViewBag.Categories"><option value="">Виберіть категорію...</option></select></div></div>
<label class="cf-check"><input type="checkbox" asp-for="RememberMe" /><span>Запам'ятати мене</span></label>
```
