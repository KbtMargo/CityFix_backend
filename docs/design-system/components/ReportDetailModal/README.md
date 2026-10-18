# ReportDetailModal
The appeal details overlay (`patterns/ReportDetailModal.tsx`). It sits on a 60% black backdrop: a white (slate-800 in dark) `rounded-2xl` box, up to 768px wide and 90vh high, that scrolls.

**Sections**
1. **Head.** Title (24px bold), then "До проблеми" (outline sm) and a round × close button. Below them, a row of Pills: status, category, priority.
2. **Фотодоказ.** A 16:9 `rounded-lg` carousel with black/30 round arrows, then thumbnails (5, 6 or 8 per row). The active thumbnail has a blue-500 2px ring; the others are at 60% opacity.
3. **Опис.** `ink-secondary` text, or "Опис відсутній."
4. **Facts.** Two columns: Місце and Повідомлено, each with a 14px semibold slate-500 label and a lucide icon.
5. **Часова шкала статусу.** See StatusTimeline.
6. **Admin footer.** A `update-panel` box titled "Оновити звернення", with a Статус select, a Коментар (опційно) textarea and a full-width "Зберегти зміни".

**In Razor.** Render it on the list page when `?reportId=` is set. Closing links back to the list. Clicking the backdrop closes it.

```cshtml
@if (Model.Report is { } r) {
<div class="cf-overlay" onclick="if(event.target===this) location.href='@Url.Action("Index")'"><div class="cf-modal" role="dialog" aria-labelledby="rt">
  <div class="cf-modal__head"><div class="cf-modal__titlebar"><h2 class="cf-modal__title" id="rt">@r.Title</h2>
    <div class="cf-modal__actions"><a class="cf-modal__close" asp-action="Index" aria-label="Закрити"><span class="cf-icon cf-icon--x-lucide cf-icon--6"></span></a></div></div>
    <div class="cf-chips"><partial name="_StatusPill" model="r.Status" /><span class="cf-pill cf-pill--neutral">@r.Category.Name</span></div></div>
  <div class="cf-modal__body">…</div>
</div></div>
}
```
