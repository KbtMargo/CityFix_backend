# Badge
A pill label from `components/ui/Badge.tsx`: `rounded-full`, padding 10px × 4px, 12px medium, with a 1px inset ring.

**Tones**
- `--neutral` (default): slate. It has dark variants.
- `--success`: emerald.
- `--warning`: amber.
- `--danger`: red.
- `--info`: sky.
- The coloured tones keep their light tints in dark mode, exactly as in the source.

**Variants (shadcn).** `--solid` (primary), `--secondary`, `--outline`.

**Do**
- Use Badge for states that are not appeal statuses: verified, role, export ready.

**Don't**
- Use Badge for appeal status or priority. Those use **Pill**, as the app does.

```cshtml
<span class="cf-badge cf-badge--success">Вирішено</span>
<span class="cf-badge cf-badge--outline">Чернетка</span>
```
