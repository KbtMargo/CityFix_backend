# KpiCard
The four KPI cards at the top of the dashboard and My appeals. Each shows a label, a large value, a trend chip with a short note, and a 7-point sparkline. Cards are refreshed from the frontend's flat `KpiCard.tsx` (on request) but keep its palette, tones and grid. The admin **StatCard** stays as in the source.

**Anatomy** (`a.cf-kpi-card.cf-kpi-card--{tone}`)
- **Top row.** `__label` (14px medium, `ink-muted`) and `__tile`, a 40px `rounded-xl` tile holding a 20px icon.
- **Value.** `__value` (30/36 semibold, −0.025em, tabular figures), with an optional `__unit` such as "дні".
- **Foot.**
  - `__trend` holds `.cf-delta` (an arrow and the change) plus a 12px note: "за тиждень", "до вересня".
  - `__spark` is an 88×32 inline SVG: a 2px line, a 12% area fill and an end dot, all in the tone colour.
- **Surface.** `surface` with `rounded-2xl`, a hairline (`line` at 70%) and `shadow-xs`. As a link it lifts 2px on hover with `shadow-card-hover`.

**Tones** (icon tile and sparkline)
| Tone | Light | Dark | Use |
| --- | --- | --- | --- |
| `--orange` | orange-600 on orange-50 | orange-500 on a 16% tint | Активні звернення |
| `--emerald` | emerald-600 on emerald-50 | emerald-500 on a 16% tint | Закрито цього місяця |
| `--slate` | slate-700 on slate-100 | slate-300 on slate-800 | Разом звернень |
| `--purple` | purple-600 on purple-50 | purple-500 on an 18% tint | Середній час відповіді |

**The delta chip says whether the change is good, not which way it goes.**
- `--good` (emerald) and `--bad` (red) carry a meaning. `--flat` (slate) is for changes that are neither.
- Fewer active appeals and a faster response are `--good` even though they point down.
- The arrow (`arrow-up` / `arrow-down`) shows direction. The colour shows meaning. Never let colour alone carry it.

**Layout.** `.cf-kpi-grid`: 1 column, 2 from 768px, 4 from 1280px. At most four cards.

**Do**
- Compute the sparkline from the last 7 periods, and compare with the same period before.
- Make the card a link to the filtered list: Активні goes to the В роботі tab.

**Don't**
- Show a delta without its note ("за тиждень").
- Add a fifth card, or change the tone of a metric between pages.

```cshtml
<div class="cf-kpi-grid">
  <a class="cf-kpi-card cf-kpi-card--orange" asp-action="Index" asp-route-tab="inprogress">
    <div class="cf-kpi-card__top"><span class="cf-kpi-card__label">Активні звернення</span><span class="cf-kpi-card__tile"><span class="cf-icon cf-icon--alert cf-icon--5"></span></span></div>
    <div class="cf-kpi-card__value">@Model.Active</div>
    <div class="cf-kpi-card__foot">
      <div class="cf-kpi-card__trend"><span class="cf-delta cf-delta--@(Model.ActiveDelta <= 0 ? "good" : "bad")"><span class="cf-icon cf-icon--arrow-@(Model.ActiveDelta <= 0 ? "down" : "up")"></span>@Math.Abs(Model.ActiveDelta)</span><span>за тиждень</span></div>
      <partial name="_Sparkline" model="Model.ActiveSeries" />  @* <svg class="cf-kpi-card__spark" viewBox="0 0 88 32"><path class="area" …/><path class="line" …/><circle class="end" …/></svg> *@
    </div>
  </a>
  @* … --emerald, --slate, --purple *@
</div>
```

**StatCard (admin, as in the source).** `.cf-statcard`: `surface-raised`, `rounded-lg`, `shadow`, 24px padding, a slate-100 / slate-700 icon tile, a 14px title and a 24px bold value. For the same look on the admin home, use `cf-kpi-card` there too.
