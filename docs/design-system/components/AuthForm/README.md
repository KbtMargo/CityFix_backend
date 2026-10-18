# AuthForm
The login and register pages: a centred white card on `auth-page` (slate-50, slate-900 in dark).

**Login card**
- 448px wide, `rounded-lg`, `shadow-md`, 32px padding, with 24px gaps.
- A centred heading: "Вхід в акаунт" (30px bold) and "Вітаємо з поверненням!".
- Fields: Email-адреса and Пароль.
- A full-width slate-800 submit, "Увійти".
- A centred 14px link line: "Ще не маєте акаунту? **Зареєструватися**".

**Register card**
- 512px wide (`--wide`).
- Ім'я and Прізвище side by side, then Email, Номер телефону (опційно), Пароль and Підтвердіть пароль side by side, then Код ОСББ.
- The reCAPTCHA goes before the submit.
- An "Або увійдіть через" divider, then Google.

**Auth inputs**
- These differ from the app's inputs: `rounded-md`, `shadow-sm`, slate-300 border.
- Fill is white (slate-700 in dark). Text is 16px on login and 14px on register at 640px and up.
- On focus the border turns slate-500.

**Errors.** One red-600 14px centred line above the button ("Невірний email або пароль", "Паролі не співпадають.").

```cshtml
<div class="cf-auth cf-auth--login"><div class="cf-auth__card">
  <div class="cf-auth__head"><h1 class="cf-auth__title">Вхід в акаунт</h1><p class="cf-auth__sub">Вітаємо з поверненням!</p></div>
  <form asp-action="Login" method="post" class="cf-stack-6">
    <div><label class="cf-auth__label" asp-for="Email">Email-адреса</label><input asp-for="Email" class="cf-auth__input" autocomplete="email" placeholder="you@example.com" /></div>
    <div><label class="cf-auth__label" asp-for="Password">Пароль</label><input asp-for="Password" class="cf-auth__input" autocomplete="current-password" placeholder="••••••••" /></div>
    @if (ViewBag.Error is string e) { <p class="cf-auth__error" role="alert">@e</p> }
    <div><button type="submit" class="cf-auth__submit">Увійти</button></div>
  </form>
  <p class="cf-auth__foot">Ще не маєте акаунту? <a class="cf-auth__link" asp-action="Register">Зареєструватися</a></p>
</div></div>
```
