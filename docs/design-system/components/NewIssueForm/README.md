# NewIssueForm
The whole `dashboard/new-issue` page:
- a 672px column, with "Створити нове звернення" (30px bold) above a white `rounded-lg` card that has a shadow and 32px padding,
- fields 24px apart: Заголовок, Детальний опис (опційно), Місцезнаходження (MapPicker), Категорія, Отримувач, Фото-докази (PhotoUpload),
- a full-width default button, "Створити звернення".

**Validation**
- Title is 5–100 characters; description is at most 1000.
- Category and recipient are required.
- Show errors as a 14px red-600 line above the button, as the source does, or under each field with `asp-validation-for`.

```cshtml
<div class="cf-form-page"><h1 class="cf-form-page__title">Створити нове звернення</h1>
  <form asp-action="Create" method="post" enctype="multipart/form-data" class="cf-form-card">
    …fields from FormField, MapPicker, PhotoUpload…
    <div asp-validation-summary="ModelOnly" class="cf-auth__error"></div>
    <button type="submit" class="cf-btn cf-btn--block">Створити звернення</button>
  </form></div>
```
