using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AcabsController : AppController
    {
        private readonly IAcabService _acabs;

        public AcabsController(IAcabService acabs, IUserService users, IStringLocalizer<SharedResource> localizer)
            : base(users, localizer)
        {
            _acabs = acabs;
        }

        public async Task<IActionResult> Index(string? q, int page = 1)
        {
            return View(new TablePageViewModel<AcabRow> { Page = await _acabs.GetPageAsync(q, page), Q = q });
        }

        public IActionResult Create() => View(new AcabFormViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Address,NameEn,AddressEn")] AcabFormViewModel form)
        {
            if (!ModelState.IsValid)
            {
                return View(form);
            }

            var acab = await _acabs.CreateAsync(form.ToInput());
            FlashSuccess(L["Acabs.Created"]);
            return RedirectToAction(nameof(Edit), new { id = acab.Id });
        }

        public async Task<IActionResult> Edit(int id) => View(AcabFormViewModel.From(await _acabs.GetAsync(id)));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address,NameEn,AddressEn")] AcabFormViewModel form)
        {
            if (id != form.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                var stored = await _acabs.GetAsync(id);
                (form.ResidentCode, form.HeadCode) = (stored.ResidentCode, stored.HeadCode);
                return View(form);
            }

            await _acabs.UpdateAsync(id, form.ToInput());
            FlashSuccess(L["Common.Saved"]);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateCode(int id, AcabCodeKind kind)
        {
            await _acabs.RegenerateCodeAsync(id, kind);
            FlashSuccess(L[kind == AcabCodeKind.Head ? "Acabs.HeadCodeRegenerated" : "Acabs.ResidentCodeRegenerated"]);
            return RedirectToAction(nameof(Edit), new { id });
        }

        public async Task<IActionResult> Delete(int id) => View(await _acabs.CheckDeleteAsync(id));

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _acabs.DeleteAsync(id);
                FlashSuccess(L["Common.Deleted"]);
                return RedirectToAction(nameof(Index));
            }
            catch (ServiceValidationException)
            {
                return RedirectToAction(nameof(Delete), new { id });
            }
        }
    }
}
