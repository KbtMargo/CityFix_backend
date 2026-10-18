# Button
The shadcn/new-york Button from `components/ui/Button.tsx`: 36px high, `rounded-md` (8px in this app), 14px medium text. There are six variants and three sizes, plus icon sizes. **AppLink** is the separate link-button from `ui/AppLink.tsx`.

| Class | Source variant | Use |
| --- | --- | --- |
| `cf-btn` | default | Main action. Fill is `primary` (near-black, near-white in dark). |
| `cf-btn--destructive` | destructive | Delete. Red fill, white text, 60% fill in dark. |
| `cf-btn--outline` | outline | Фільтри, date picker, "До проблеми", icon buttons. |
| `cf-btn--secondary` | secondary | Grey fill. |
| `cf-btn--ghost` | ghost | Скинути, popover close. |
| `cf-btn--link` | link | Text link styled as a button. |

**Sizes**
- `--sm`: 32px.
- Default: 36px.
- `--lg`: 40px.
- `--icon`: 36px square. Also `--icon-sm` and `--icon-lg`.
- `--round` makes it a circle, `--block` makes it full width.

**Icons**
- A leading icon tightens the side padding to 12px (`has-[>svg]:px-3`).
- Icons are 16px.
- Add `cf-btn--icon-gap` where the source adds `mr-2` to the icon (Фільтри, Topbar, saving spinners).

**Filter count.** `.cf-btn__count` is the blue-600 circle on the top-right corner.

**Focus.** On focus the border turns `ring` with a 3px `ring` halo at 50%. Disabled buttons are 50% opacity and ignore clicks.

**AppLink**
- `.cf-applink` is inverse (slate-900 / white), `rounded-lg`, padding 16px × 8px.
- `--soft` is a slate-300 ring.

**Do**
- Have one default (dark) button per view.
- Label buttons with a verb: "Створити звернення", "Зберегти зміни".

**Don't**
- Use `--destructive` outside confirmation dialogs.
- Mix AppLink and Button in one row unless the source does (the landing CTA does).

```cshtml
<button type="submit" class="cf-btn cf-btn--block">Створити звернення</button>
<button type="button" class="cf-btn cf-btn--outline cf-btn--icon-gap"><span class="cf-icon cf-icon--sliders-horizontal cf-icon--4"></span>Фільтри<span class="cf-btn__count">2</span></button>
<button type="button" class="cf-btn cf-btn--outline cf-btn--icon" aria-label="Визначити поточне місцезнаходження"><span class="cf-icon cf-icon--locate-fixed cf-icon--4"></span></button>
<a class="cf-applink" asp-controller="Dashboard" asp-action="Index">Перейти до панелі</a>
```
