using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
    // Administrators manage every account; association heads see the members of their own association.
    // The rules (who may do what, last administrator, unique email) are in UserService.
    // Passwords are never typed here: Register sets them, or the admin issues a one-time temporary password.
    [Authorize(Roles = "Administrator,AcabAdmin")]
    public class UsersController : AppController
    {
        private const string FormFields = "FirstName,LastName,Email,Phone,Role,AcabId";

        private readonly ILookupService _lookups;

        public UsersController(ILookupService lookups, IUserService users, IStringLocalizer<SharedResource> localizer)
            : base(users, localizer)
        {
            _lookups = lookups;
        }

        // GET: Users?q=
        public async Task<IActionResult> Index(string? q, int page = 1)
        {
            var actor = await GetActorAsync();
            return View(new UsersIndexViewModel
            {
                Data = await Users.GetPageAsync(actor, q, page),
                Q = q,
                IsAdministrator = actor.Role == UserRole.Administrator,
                CurrentUserId = actor.Id
            });
        }

        // GET: Users/Create
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Create()
        {
            return View(await WithAcabsAsync(new UserFormViewModel()));
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Create([Bind(FormFields)] UserFormViewModel form)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var (user, password) = await Users.CreateAsync(form.ToInput(), await GetActorAsync());
                    // Shown once, never stored in plain text.
                    FlashSuccess(L["Users.CreatedWithPassword", user.Email, password]);
                    return RedirectToAction(nameof(Index));
                }
                catch (ServiceValidationException ex)
                {
                    AddModelError(ex);
                }
            }
            return View(await WithAcabsAsync(form));
        }

        // GET: Users/Edit/5
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Edit(int id)
        {
            return View(await WithAcabsAsync(UserFormViewModel.From(await Users.GetAsync(id))));
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Edit(int id, [Bind("Id," + FormFields)] UserFormViewModel form)
        {
            if (id != form.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    await Users.UpdateAsync(id, form.ToInput(), await GetActorAsync());
                    FlashSuccess(L["Common.Saved"]);
                    return RedirectToAction(nameof(Index));
                }
                catch (ServiceValidationException ex)
                {
                    AddModelError(ex);
                }
            }

            form.IsBlocked = (await Users.GetAsync(id)).IsBlocked;
            return View(await WithAcabsAsync(form));
        }

        // POST: Users/ResetPassword/5  (a new one-time temporary password)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var user = await Users.GetAsync(id);
            var password = await Users.ResetPasswordAsync(id, await GetActorAsync());
            FlashSuccess(L["Users.PasswordReset", user.Email, password]);
            return RedirectToAction(nameof(Edit), new { id });
        }

        // POST: Users/SetBlocked/5?blocked=true|false
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> SetBlocked(int id, bool blocked)
        {
            try
            {
                await Users.SetBlockedAsync(id, blocked, await GetActorAsync());
                FlashSuccess(L[blocked ? "Users.Blocked" : "Users.Unblocked"]);
            }
            catch (ServiceValidationException ex)
            {
                FlashError(Message(ex));
            }
            return RedirectToAction(nameof(Edit), new { id });
        }

        // GET: Users/Delete/5  (confirmation dialog; blocked while the user has appeals or history)
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(int id)
        {
            return View(await Users.CheckDeleteAsync(id, await GetActorAsync()));
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await Users.DeleteAsync(id, await GetActorAsync());
                FlashSuccess(L["Common.Deleted"]);
                return RedirectToAction(nameof(Index));
            }
            catch (ServiceValidationException)
            {
                return RedirectToAction(nameof(Delete), new { id });
            }
        }

        private async Task<UserFormViewModel> WithAcabsAsync(UserFormViewModel form)
        {
            form.Acabs = (await _lookups.GetAsync()).Acabs;
            return form;
        }
    }
}
