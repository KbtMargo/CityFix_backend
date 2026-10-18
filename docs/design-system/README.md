CivicReport is a community reporting platform. Residents report problems, OSBB heads and administrators track them. Its interface is the CivicReport frontend (Next.js, Tailwind v4, shadcn new-york), reproduced here as plain CSS for the Razor views. It is quiet and monochrome: slate surfaces, near-black selected states, soft 50-tint pills, and very few shadows. Every token value and every class is copied from that codebase; where a value differs, the component README says so.

## Using it in the Razor app

1. Copy `tokens.css` (generated from `tokens.json`) and `components/bundle.css` into `wwwroot/css/`, and link them in that order in `_Layout.cshtml`. There are no web fonts to copy (see Type).
2. Light theme is the default. For dark, set `data-theme="dark"` on `<html>`; the frontend toggles a `.dark` class instead.
3. Wrap pages in `.cf-shell`:
   - residents: a 320px white sidebar;
   - admin and OSBB: `.cf-shell--admin`, a 280px column and a slate-100 main area with 32px padding.
4. `bundle.css` starts with the same preflight as Tailwind v4 (box-sizing, zero margins, borders default to `--border`). Views that rely on browser default margins will look different after linking it.

## Content

- **Language.** All interface copy is Ukrainian, short and neutral: "Створити нове звернення", "Нічого не знайдено за поточними фільтрами.", "Вітаємо з поверненням!".
- **Casing.** Sentence case everywhere. Exceptions: the admin table headers are uppercase by CSS, and the wordmark is "CivicReport".
- **Formats.**
  - Dates use `dd.MM.yyyy` in lists and `dd.MM.yyyy, HH:mm:ss` in the modal timeline.
  - Relative times: "Щойно", "N год тому", "N д тому".
- **Names in the UI.** Appeals are *звернення*, housing associations are *ОСББ*, recipients are *установа* or *отримувач*.
- **Loading.** Show loading as one line of text: "Завантаження...", "Перевірка доступу...".
- **No decoration in copy.** No emoji, no exclamation marks, except the source's two greetings ("Вітаємо з поверненням!", "Все переглянуто!").

## Colour

- **Surfaces.**
  - The page is `page` (slate-50; slate-950 in dark). Admin main areas use `page-admin` (slate-100).
  - Cards, issue items, tab strips and the sidebar use `surface` (white / slate-900).
  - Modals, tables, the auth card and the new-issue form use `surface-raised` (white / slate-800).
  - Quiet boxes (sidebar stats, help) use `surface-muted` with a `line` ring.
- **The selected state is inverse, not a hue.** The active nav icon tile, the active tab, AppLink, and the EmptyState tile are `inverse` (slate-900, white in dark) with `on-inverse` text. The shadcn Button uses `primary` (oklch 0.205 0 0), which is nearly the same black.
- **Text.**
  - `ink` (slate-900 / slate-100) for content.
  - `ink-secondary` (slate-600) for excerpts and descriptions.
  - `ink-muted` (slate-500) for meta, subtitles and hints.
  - `ink-label` (slate-700) for field labels.
- **Hue appears only as 50-tint pills and badges with a 200 ring and 700 text**:
  - red: new and urgent,
  - amber: in progress and medium,
  - emerald: success,
  - sky: info,
  - slate: done, rejected and categories.
- **Small colour accents.**
  - Dots: orange-500 for open, emerald-500 for closed.
  - The filter count is blue-600, the upload link blue-600, and the active photo ring blue-500.
  - Admin role tags use 100 fills: admin red, OSBB head blue, resident green.
- **Charts** use the Recharts hex series from the analytics pages (`series-*`): #3B82F6 first.
- **Dark mode.** Most coloured tints stay light, exactly as in the source.
- **Contrast notes, kept from the source and flagged on their tokens.**
  - The placeholder (slate-400 on white) is 2.6:1.
  - The input border (slate-300 on white) is 1.5:1.
  - `ink-muted` on white is 4.8:1 and passes.

## Type

- **Family.** There is no web font. The frontend declares Geist (`--font-geist-sans`) but never loads it, so Tailwind falls back to the system stack, and that is what users actually see: `-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", "Noto Sans", Arial, sans-serif`. This is `--font-sans`. Every face in it covers Cyrillic.
- **Scale** (Tailwind):

  | Style | Size / line-height | Weight | Use |
  | --- | --- | --- | --- |
  | `cf-display` | 36/40 | bold, −0.025em | Dashboard "Панель" |
  | `cf-h1` | 30/36 | bold | Page and auth titles |
  | `cf-h2` | 24/32 | bold | Modal title |
  | `cf-h3` | 18/28 | semibold | Section and issue titles |
  | `cf-logo` | 20/28 | bold, −0.05em | Wordmark |
  | `cf-kpi` | 24/32 | semibold, −0.025em | KPI values |
  | `cf-body` | 16/24 | regular | Running text |
  | `cf-small` | 14/20 | regular | Meta, inputs, buttons |
  | `cf-label` | 14/20 | medium | Field labels |
  | `cf-caption` | 12/16 | regular | Captions |
  | `cf-badge` | 12/16 | medium | Pills and badges |

- **Weights.** Only 400, 500, 600 and 700.

## Space, shape, depth

- **Spacing.** Tailwind's 4px scale, named the same: `space-1` is 4px, `space-4` 16, `space-6` 24 and so on.
  - Pages use 24px padding with 24px between blocks.
  - Cards use 20px padding. Forms and auth cards use 32px.
  - Lists are 16px apart, fields 24px.
- **Radii.** shadcn redefines the `rounded-*` steps in this app (`--radius` is 10px). Copy them exactly:

  | Token | Value | Use |
  | --- | --- | --- |
  | `radius-md` | 8px | Button, Select, auth inputs |
  | `radius-lg` | 10px | Tabs, nav tiles, tables, alerts |
  | `radius-xl` | 14px | Inputs, nav items, boxes, KPI tiles |
  | `radius-2xl` | 16px | Cards, the modal, empty states |
  | `radius-full` | — | Pills, badges, dots |

- **Shadows.** Cards are flat and lift on hover (`shadow-card-hover`, a slate-tinted `shadow-md`). Tables, StatCards and the form card use `shadow-sm`. The auth card, popovers and lists use `shadow-md`. Dialogs use `shadow-lg`, admin modals `shadow-xl`.
- **Motion.** 150ms colour and shadow transitions, a 200ms Card lift, `scale-105` on the logo, and a zoom-in (95%) on modals.
- **Focus.**
  - Buttons get a 3px `ring` at 50%.
  - Inputs get a 2px slate-300 ring.
  - Selects get a 2px slate-400 ring with a 2px offset.

## Iconography

- **Two sets.**
  - The app's own 16 icons from `Icons.tsx` (24 grid, stroke 2): home, users, list, plus, bars, bell, shield, pin, clock, camera, check, alert, x, arrow-right, apartment, chart-bar.
  - **Lucide** icons (stroke 2, round caps) for UI chrome: chevrons, map-pin, calendar, upload, locate-fixed, sliders-horizontal, edit, trash, file-text, loader and so on.
- **Use.** Both work as `<span class="cf-icon cf-icon--name">`, which takes `currentColor`. Sizes: `--4` is 16px (buttons and meta), `--5` 20px (nav, default), `--6` 24px (KPI tiles).
- **Files.** The 16 own icons are also in the Icons asset group (ink #0f172a), for documents.

## Brand mark

- **The mark.** The `logo` SVG from `Icons.tsx`: an L-shaped axis with three bars, white on a #0C141F tile with 12px radius, 45px.
- **The lockup** (`cf-logo`): the mark, then **CivicReport** (20px bold, −0.05em) over *Community Platform* (12px slate-500).
- **File.** `assets/Logos/civicreport-mark.svg` is the exact file. Inline it in views; don't redraw it.
