# Alert
The app's inline feedback.

- `.cf-alert`: 12px padding, red-50 fill, red-200 border, `rounded-lg`, red-700 14px text. Use it for server errors inside forms and modals.
- `--box` adds a red-800 title.
- `.cf-auth__error`: a single red-600 14px line.
- `.cf-success-text`: green-600 12px, such as "Координати обрано".
- `.cf-denied` is the "Доступ заборонено" panel: red-50 (red-900/20 in dark), a red title and a red-600 button.

The source also uses `sonner` toasts for success ("Реєстрація успішна! Тепер ви можете увійти."). In Razor, show those as a `cf-badge--success` line or a TempData message. Toasts are not part of this system.

```cshtml
@if (TempData["Error"] is string err) { <div class="cf-alert" role="alert">@err</div> }
```
