using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
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

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Create()
        {
            return View(await WithAcabsAsync(new UserFormViewModel()));
        }

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

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Edit(int id)
        {
            return View(await WithAcabsAsync(UserFormViewModel.From(await Users.GetAsync(id))));
        }

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

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(int id)
        {
            return View(await Users.CheckDeleteAsync(id, await GetActorAsync()));
        }

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
