using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
    [Authorize]
    public class AppealsController : AppController
    {
        private readonly IAppealService _appeals;
        private readonly IFileService _files;
        private readonly IStringLocalizer<SharedResource> _l;

        public AppealsController(IAppealService appeals, IFileService files, IStringLocalizer<SharedResource> localizer)
        {
            _appeals = appeals;
            _files = files;
            _l = localizer;
        }

        // GET: Appeals  ("My appeals" for residents, "All appeals" for staff; ?appealId= opens the detail modal)
        public async Task<IActionResult> Index([FromQuery] AppealFilter filter, int? appealId)
        {
            var user = await _appeals.GetUserAsync(CurrentUserId);
            filter.MineOnly = user.Role == UserRole.Resident;

            var model = await BuildListAsync(filter, appealId, user, "Appeals");
            if (appealId != null && model.Selected == null)
            {
                return NotFound();
            }
            return View(model);
        }

        // GET: Appeals/Details/5 → the list with the modal open
        public IActionResult Details(int id) => RedirectToAction(nameof(Index), new { appealId = id });

        // GET: Appeals/Create
        public async Task<IActionResult> Create()
        {
            await PopulateFormAsync(null);
            return View(new Appeal());
        }

        // POST: Appeals/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(60 * 1024 * 1024)]
        public async Task<IActionResult> Create([Bind("Title,Description,Lat,Lng,Address,CategoryId,RecipientId")] Appeal appeal, List<IFormFile> photos)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Check the photos first, so a bad file doesn't leave an appeal without its photos.
                    _files.ValidatePhotos(photos);
                    await _appeals.CreateAsync(appeal, CurrentUserId);
                    if (photos.Count > 0)
                    {
                        await _files.SaveAppealPhotosAsync(appeal.Id, photos);
                    }
                    FlashSuccess(_l["Appeal.Created"]);
                    return RedirectToAction(nameof(Index), new { appealId = appeal.Id });
                }
                catch (ServiceValidationException ex)
                {
                    ModelState.AddModelError(ex.Field, _l[ex.MessageKey]);
                }
            }

            await PopulateFormAsync(appeal);
            return View(appeal);
        }

        // GET: Appeals/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var (appeal, error) = await LoadAsync(id);
            if (appeal == null)
            {
                return error!;
            }
            await PopulateFormAsync(appeal);
            return View(appeal);
        }

        // POST: Appeals/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(60 * 1024 * 1024)]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,Lat,Lng,Address,CategoryId,RecipientId,Priority")] Appeal appeal, List<IFormFile> photos)
        {
            if (id != appeal.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _files.ValidatePhotos(photos, await _files.CountAppealPhotosAsync(id));
                    if (!await _appeals.UpdateAsync(appeal, CurrentUserId))
                    {
                        return NotFound();
                    }
                    if (photos.Count > 0)
                    {
                        await _files.SaveAppealPhotosAsync(id, photos);
                    }
                    FlashSuccess(_l["Appeal.Saved"]);
                    return RedirectToAction(nameof(Index), new { appealId = id });
                }
                catch (UnauthorizedAccessException)
                {
                    return Forbid();
                }
                catch (ServiceValidationException ex)
                {
                    ModelState.AddModelError(ex.Field, _l[ex.MessageKey]);
                }
            }

            var stored = await _appeals.GetByIdAsync(id, CurrentUserId);
            appeal.Files = stored?.Files ?? new();
            await PopulateFormAsync(appeal);
            return View(appeal);
        }

        // POST: Appeals/UpdateStatus  (the "Update appeal" panel in the detail modal)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int appealId, AppealStatus status, string? comment, string? returnUrl)
        {
            try
            {
                await _appeals.UpdateStatusAsync(appealId, status, comment, CurrentUserId);
                FlashSuccess(_l["Appeal.StatusSaved"]);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ServiceValidationException ex)
            {
                // Shown inside the modal as a cf-alert; the comment is kept so it isn't lost.
                FlashError(_l[ex.MessageKey]);
                TempData["Comment"] = comment;
                TempData["Status"] = status.ToString();
            }

            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction(nameof(Index), new { appealId });
        }

        // GET: Appeals/Delete/5  (confirmation dialog)
        public async Task<IActionResult> Delete(int id)
        {
            var (appeal, error) = await LoadAsync(id);
            return appeal != null ? View(appeal) : error!;
        }

        // POST: Appeals/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _appeals.DeleteAsync(id, CurrentUserId);
                _files.DeleteAppealFolder(id);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }

            FlashSuccess(_l["Appeal.Deleted"]);
            return RedirectToAction(nameof(Index));
        }

        // The appeal the current user may access, or the 404 / 403 result to return instead.
        private async Task<(Appeal? Appeal, IActionResult? Error)> LoadAsync(int id)
        {
            try
            {
                var appeal = await _appeals.GetByIdAsync(id, CurrentUserId);
                return appeal != null ? (appeal, null) : (null, NotFound());
            }
            catch (UnauthorizedAccessException)
            {
                return (null, Forbid());
            }
        }

        private Task<AppealListViewModel> BuildListAsync(AppealFilter filter, int? appealId, User user, string controller) =>
            AppealListFactory.BuildAsync(_appeals, filter, appealId, user, controller);

        private async Task PopulateFormAsync(Appeal? appeal)
        {
            var lookups = await _appeals.GetLookupsAsync();
            var user = await _appeals.GetUserAsync(CurrentUserId);
            ViewData["IsStaff"] = user.Role != UserRole.Resident;
            ViewData["CategoryId"] = new SelectList(lookups.Categories, "Id", "Name", appeal?.CategoryId);
            ViewData["RecipientId"] = new SelectList(lookups.Recipients, "Id", "Name", appeal?.RecipientId);
        }
    }
}
