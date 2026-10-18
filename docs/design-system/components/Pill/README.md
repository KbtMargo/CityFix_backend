# Pill
The severity pill from `components/ui/Pill.tsx`, used for appeal status, priority and the category tag. It shares Badge's shape (`rounded-full`, padding 10px × 4px, 12px medium, inset ring), with four levels.

| Level | Colours | Mapped from (ReportDetailModal) |
| --- | --- | --- |
| `--high` | red-700 on red-50, ring red-200 | Status NEW (`--new`), priority URGENT (`--urgent`, "високий пріоритет") |
| `--medium` | amber-700 on amber-50, ring amber-200 | Status IN_PROGRESS (`--inprogress`), priority NORMAL (`--normal`, "середній пріоритет") |
| `--low` | slate-600 on slate-100, ring slate-200 | Status DONE (`--done`), priority LOW ("низький пріоритет") |
| `--neutral` | same as low | Status REJECTED (`--rejected`), category name |

The modifiers `--new`, `--inprogress`, `--done` and `--rejected` are named after the enum (lower-case, with the underscore removed), so Razor can build them straight from `Status`.

**Do**
- Show Ukrainian labels: Нове, В роботі, Виконано, Відхилено. The source modal prints the raw enum; that is the one thing changed here.

**Don't**
- Add colours. DONE and REJECTED are deliberately grey.

```cshtml
<span class="cf-pill cf-pill--@Model.Status.ToString().Replace("_", "").ToLowerInvariant()">@Model.Status.GetDisplayName()</span>
<span class="cf-pill cf-pill--neutral">@Model.Category.Name</span>
```
