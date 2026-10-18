# Loading
- **Skeleton** (`ui/Skeleton.tsx`): a `rounded-md` block in `skeleton` (slate-200/80, slate-700/50 in dark) that pulses (2s). The sidebar uses 40px, 80px and 48px blocks while auth loads.
- **Spinner** (`ui/Spinner.tsx`): a 20px circle with a 2px `currentColor` border and a transparent right side, spinning once per second. In buttons the source uses the lucide `loader` icon with `cf-spin`.
- **Progress** (`ui/Progress.tsx`): an 8px full-round track (slate-200 / slate-700) with an inverse bar.
  - Sizes `--sm` (4px) and `--lg` (12px).
  - Coloured bars `--blue`, `--green`, `--red`.
  - `--multi` takes segment children.
- **Page loading.** `.cf-loading` is plain centred text with 40px padding ("Завантаження ваших звернень..."), as on every page of the source.

```cshtml
<div class="cf-loading">Завантаження...</div>
<button class="cf-btn cf-btn--icon-gap" disabled><span class="cf-icon cf-icon--loader cf-icon--4 cf-spin"></span>Збереження...</button>
```
