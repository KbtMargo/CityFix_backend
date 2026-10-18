using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
    // Condominium associations (ОСББ) and their invitation codes. The entity stays "Acab" in code and in the database.
    [Authorize(Roles = "Administrator")]
    public class AcabsController : AppController
    {
        private readonly IAcabService _acabs;

        public AcabsController(IAcabService acabs, IUserService users, IStringLocalizer<SharedResource> localizer)
            : base(users, localizer)
        {
            _acabs = acabs;
        }

        // GET: Acabs?q=
        public async Task<IActionResult> Index(string? q, int page = 1)
        {
            return View(new TablePageViewModel<AcabRow> { Page = await _acabs.GetPageAsync(q, page), Q = q });
        }

        // GET: Acabs/Create
        public IActionResult Create() => View(new AcabFormViewModel());

        // POST: Acabs/Create  (both codes are generated)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Address")] AcabFormViewModel form)
        {
            if (!ModelState.IsValid)
            {
                return View(form);
            }

            var acab = await _acabs.CreateAsync(form.ToInput());
            FlashSuccess(L["Acabs.Created"]);
            return RedirectToAction(nameof(Edit), new { id = acab.Id });
        }

        // GET: Acabs/Edit/5
        public async Task<IActionResult> Edit(int id) => View(AcabFormViewModel.From(await _acabs.GetAsync(id)));

        // POST: Acabs/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address")] AcabFormViewModel form)
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

        // POST: Acabs/RegenerateCode/5?kind=Resident|Head  (a leaked code is replaced)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateCode(int id, AcabCodeKind kind)
        {
            await _acabs.RegenerateCodeAsync(id, kind);
            FlashSuccess(L[kind == AcabCodeKind.Head ? "Acabs.HeadCodeRegenerated" : "Acabs.ResidentCodeRegenerated"]);
            return RedirectToAction(nameof(Edit), new { id });
        }

        // GET: Acabs/Delete/5  (blocked while users or appeals belong to it)
        public async Task<IActionResult> Delete(int id) => View(await _acabs.CheckDeleteAsync(id));

        // POST: Acabs/Delete/5
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
