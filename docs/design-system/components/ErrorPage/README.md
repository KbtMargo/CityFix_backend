# ErrorPage
**Addition.** The frontend falls back to Next.js's default 404. This page is built from the app's own EmptyState and AppLink:
- an inverse tile with the alert icon,
- the code (48px bold, −0.05em),
- the title and one line of text,
- a soft AppLink "Назад" and an AppLink "Перейти до панелі".

**Copy**
- **404:** "Сторінку не знайдено" / "Можливо, посилання застаріле або звернення було видалено."
- **403:** use the **Alert** "Доступ заборонено" panel instead, as the admin pages do.

```cshtml
<div class="cf-error-page"><div class="cf-empty"><div class="cf-empty__tile"><span class="cf-icon cf-icon--alert cf-icon--6"></span></div>
  <p class="cf-error-page__code">404</p><div class="cf-empty__title">Сторінку не знайдено</div>
  <div class="cf-empty__text">Можливо, посилання застаріле або звернення було видалено.</div>
  <div class="cf-error-page__actions"><a class="cf-applink cf-applink--soft" href="javascript:history.back()"><span class="cf-icon cf-icon--arrow-left cf-icon--4"></span>Назад</a><a class="cf-applink" href="/dashboard">Перейти до панелі</a></div></div></div>
```
