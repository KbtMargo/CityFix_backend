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
        private readonly IAuditLog _audit;
        private readonly IAppealService _appeals;

        public UsersController(ILookupService lookups, IUserService users, IStringLocalizer<SharedResource> localizer, IAuditLog audit,
            IAppealService appeals)
            : base(users, localizer)
        {
            _lookups = lookups;
            _audit = audit;
            _appeals = appeals;
        }

        private static string Target(User user) => $"User #{user.Id} {user.Email}";

        // acabId narrows the list to one association's members; heads always see only their own, so it is ignored for them.
        public async Task<IActionResult> Index(string? q, int page = 1, int? acabId = null)
        {
            var actor = await GetActorAsync();
            return View(new UsersIndexViewModel
            {
                Data = await Users.GetPageAsync(actor, q, page, acabId),
                Q = q,
                IsAdministrator = actor.Role == UserRole.Administrator,
                CurrentUserId = actor.Id
            });
        }

        public async Task<IActionResult> Details(int id, int page = 1)
        {
            var actor = await GetActorAsync();
            return View(new UserDetailsViewModel
            {
                User = await Users.GetForViewAsync(id, actor),
                Appeals = await _appeals.GetByAuthorAsync(id, actor, page),
                IsAdministrator = actor.Role == UserRole.Administrator
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
                    await _audit.RecordAsync(await GetActorAsync(), "user.create", Target(user), $"role={user.Role}, acab={user.AcabId}");
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
                    var before = await Users.GetAsync(id);
                    await Users.UpdateAsync(id, form.ToInput(), await GetActorAsync());
                    var after = await Users.GetAsync(id);
                    await _audit.RecordAsync(await GetActorAsync(), "user.update", Target(after),
                        $"role {before.Role}→{after.Role}, acab {before.AcabId}→{after.AcabId}, email {before.Email}→{after.Email}");
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
            await _audit.RecordAsync(await GetActorAsync(), "user.reset-password", Target(user));
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
                await _audit.RecordAsync(await GetActorAsync(), blocked ? "user.block" : "user.unblock", Target(await Users.GetAsync(id)));
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
                var target = Target(await Users.GetAsync(id));
                await Users.DeleteAsync(id, await GetActorAsync());
                await _audit.RecordAsync(await GetActorAsync(), "user.delete", target);
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
