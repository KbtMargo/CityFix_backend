using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using CityFix_backend.Helpers;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.Services.Reports;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
    [Authorize]
    public class AppealsController : AppController
    {
        private const string FormFields = "Title,Description,Lat,Lng,Address,CategoryId,Responsible";

        private readonly IAppealService _appeals;
        private readonly ILookupService _lookups;
        private readonly IFileService _files;

        public AppealsController(IAppealService appeals, ILookupService lookups, IFileService files,
            IUserService users, IStringLocalizer<SharedResource> localizer) : base(users, localizer)
        {
            _appeals = appeals;
            _lookups = lookups;
            _files = files;
        }

        // GET: Appeals  ("My appeals" for residents, "All appeals" for staff; ?appealId= opens the detail modal)
        public async Task<IActionResult> Index([FromQuery] AppealFilter filter, int? appealId)
        {
            var actor = await GetActorAsync();
            filter.MineOnly = actor.Role == UserRole.Resident;
            return View(await AppealListFactory.BuildAsync(_appeals, _lookups, filter, appealId, actor, "Appeals"));
        }

        // GET: Appeals/Details/5 → the list with the modal open
        public IActionResult Details(int id) => RedirectToAction(nameof(Index), new { appealId = id });

        // GET: Appeals/Create
        public async Task<IActionResult> Create()
        {
            var actor = await GetActorAsync();
            var form = new AppealFormViewModel { Responsible = actor.AcabId != null ? AppealFilter.ResponsibleAcab : null };
            await PopulateAsync(form, actor.AcabId);
            return View(form);
        }

        // POST: Appeals/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(60 * 1024 * 1024)]
        public async Task<IActionResult> Create([Bind(FormFields)] AppealFormViewModel form, List<IFormFile> photos)
        {
            var actor = await GetActorAsync();
            if (ModelState.IsValid)
            {
                try
                {
                    var appeal = await _appeals.CreateAsync(form.ToInput(), photos, actor);
                    FlashSuccess(L["Appeal.Created"]);
                    return RedirectToAction(nameof(Index), new { appealId = appeal.Id });
                }
                catch (ServiceValidationException ex)
                {
                    AddModelError(ex);
                }
            }

            await PopulateAsync(form, actor.AcabId);
            return View(form);
        }

        // GET: Appeals/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var appeal = await _appeals.GetForEditAsync(id, await GetActorAsync());
            var form = AppealFormViewModel.From(appeal);
            await PopulateAsync(form, appeal.AcabId);
            return View(form);
        }

        // POST: Appeals/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(60 * 1024 * 1024)]
        public async Task<IActionResult> Edit(int id, [Bind("Id," + FormFields)] AppealFormViewModel form, List<IFormFile> photos)
        {
            if (id != form.Id)
            {
                return NotFound();
            }

            var actor = await GetActorAsync();
            if (ModelState.IsValid)
            {
                try
                {
                    await _appeals.UpdateAsync(id, form.ToInput(), photos, actor);
                    FlashSuccess(L["Appeal.Saved"]);
                    return RedirectToAction(nameof(Index), new { appealId = id });
                }
                catch (ServiceValidationException ex)
                {
                    AddModelError(ex);
                }
            }

            // Same access check as the GET: a user who may not edit gets 403, not the form.
            var stored = await _appeals.GetForEditAsync(id, actor);
            form.Files = stored.Files;
            await PopulateAsync(form, stored.AcabId);
            return View(form);
        }

        // POST: Appeals/Process  (the "Update appeal" panel: status, priority, comment)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(int appealId, AppealStatus expectedStatus, AppealStatus status,
            AppealPriority priority, string? comment, string? returnUrl)
        {
            try
            {
                await _appeals.ProcessAsync(appealId, new AppealProcessInput(expectedStatus, status, priority, comment), await GetActorAsync());
                FlashSuccess(L["Appeal.StatusSaved"]);
            }
            catch (ServiceValidationException ex)
            {
                // Shown inside the modal as a cf-alert; the typed values are kept so they aren't lost.
                FlashError(Message(ex));
                TempData["Comment"] = comment;
                TempData["Status"] = status.ToString();
                TempData["Priority"] = priority.ToString();
            }

            return RedirectToLocal(returnUrl, () => RedirectToAction(nameof(Index), new { appealId }));
        }

        // POST: Appeals/Assign  (the responsible party: the association or a recipient)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int appealId, string? responsible, string? returnUrl)
        {
            var form = new AppealFormViewModel { Responsible = responsible };
            var input = form.ToInput();
            try
            {
                await _appeals.AssignAsync(appealId, input.Responsible, input.RecipientId, await GetActorAsync());
                FlashSuccess(L["Appeal.Assigned"]);
            }
            catch (ServiceValidationException ex)
            {
                FlashError(Message(ex));
            }

            return RedirectToLocal(returnUrl, () => RedirectToAction(nameof(Index), new { appealId }));
        }

        // GET: Appeals/Delete/5  (confirmation dialog)
        public async Task<IActionResult> Delete(int id)
        {
            var details = await _appeals.GetDetailsAsync(id, await GetActorAsync());
            return details.CanDelete
                ? View(new DeleteCheck(id, details.Appeal.DisplayTitle(), 0, null))
                : Forbid();
        }

        // POST: Appeals/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _appeals.DeleteAsync(id, await GetActorAsync());
            FlashSuccess(L["Appeal.Deleted"]);
            return RedirectToAction(nameof(Index));
        }

        // GET: Appeals/Similar?lat=&lng=&categoryId=  (the create form's duplicate warning, JSON)
        [HttpGet]
        public async Task<IActionResult> Similar(double lat, double lng, int categoryId, int? excludeId)
        {
            if (lat is < -90 or > 90 || lng is < -180 or > 180)
            {
                return BadRequest();
            }

            var similar = await _appeals.FindSimilarAsync(lat, lng, categoryId, excludeId, await GetActorAsync());
            return Json(similar.Select(s => new
            {
                s.Id,
                s.Title,
                Status = s.Status.GetDisplayName(),
                Date = s.CreatedAt.ToDate(),
                Distance = L["Appeal.Distance", s.DistanceMeters].Value,
                Url = s.Visible ? Url.Action(nameof(Index), new { appealId = s.Id }) : null
            }));
        }

        // GET: Appeals/Export?format=xlsx|pdf|csv|json&...filters  (staff: the appeals of the current filter)
        [HttpGet]
        public async Task<IActionResult> Export([FromQuery] AppealFilter filter, string format = "xlsx")
        {
            var actor = await GetActorAsync();
            if (!actor.IsStaff)
            {
                return Forbid();
            }

            var rows = await _appeals.ExportAsync(filter, actor);
            var now = DateTime.UtcNow;

            switch (format)
            {
                case "json":
                    var json = JsonSerializer.SerializeToUtf8Bytes(rows, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
                    });
                    return File(json, "application/json", ReportHeader.FileName("appeals", now, "json"));
                case "csv":
                    return File(Csv(rows), "text/csv", ReportHeader.FileName("appeals", now, "csv"));
            }

            var header = await ReportHeaderAsync(L["Report.Appeals.Title"], await FilterSummaryAsync(filter));
            return format == "pdf"
                ? File(AppealReports.Pdf(rows, header), "application/pdf", ReportHeader.FileName("appeals", now, "pdf"))
                : File(AppealReports.Xlsx(rows, header), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    ReportHeader.FileName("appeals", now, "xlsx"));
        }

        // "Tab: New · Category: Elevators · …" for the report header; null when nothing narrows the list.
        private async Task<string?> FilterSummaryAsync(AppealFilter filter)
        {
            var lookups = await _lookups.GetAsync();
            var parts = new List<string>();
            if (filter.CurrentTab != AppealFilter.TabAll)
            {
                var tab = filter.CurrentTab switch
                {
                    AppealFilter.TabNew => "Tabs.New",
                    AppealFilter.TabInProgress => "Tabs.InProgress",
                    _ => "Tabs.Closed"
                };
                parts.Add($"{L["Tabs.Label"]}: {L[tab]}");
            }
            if (!string.IsNullOrWhiteSpace(filter.Q)) parts.Add($"{L["Report.Search"]}: «{filter.Q.Trim()}»");
            if (filter.From != null || filter.To != null)
            {
                parts.Add($"{L["Report.Period"]}: {filter.From?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? "…"} – {filter.To?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? "…"}");
            }
            if (filter.CategoryId != null)
            {
                parts.Add($"{L["Field.Category"]}: {lookups.Categories.FirstOrDefault(c => c.Id == filter.CategoryId)?.DisplayName()}");
            }
            if (filter.ResponsibleIsAcab)
            {
                parts.Add($"{L["Field.Responsible"]}: {L["Enum.ResponsibleKind.Association"]}");
            }
            else if (filter.ResponsibleRecipientId is int recipientId)
            {
                parts.Add($"{L["Field.Responsible"]}: {lookups.Recipients.FirstOrDefault(r => r.Id == recipientId)?.DisplayName()}");
            }
            if (filter.AcabId != null)
            {
                parts.Add($"{L["Field.Acab"]}: {lookups.Acabs.FirstOrDefault(a => a.Id == filter.AcabId)?.DisplayName()}");
            }
            if (filter.Priority is AppealPriority priority)
            {
                parts.Add($"{L["Field.Priority"]}: {priority.GetDisplayName()}");
            }
            return parts.Count == 0 ? null : $"{L["Report.Filters"]}: {string.Join(" · ", parts)}";
        }

        // RFC 4180 CSV with a UTF-8 BOM, so Excel opens the Cyrillic text correctly.
        private byte[] Csv(IReadOnlyList<AppealExportRow> rows)
        {
            static string Cell(object? value)
            {
                var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                return text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
            }

            var csv = new StringBuilder();
            csv.AppendLine(string.Join(",", new[]
            {
                "Id", L["Field.Title"], L["Field.Status"], L["Field.Priority"], L["Field.Category"], L["Field.Responsible"],
                L["Field.Acab"], L["Field.Author"], L["Field.Address"], L["Field.Latitude"], L["Field.Longitude"], L["Field.CreatedAt"]
            }.Select(h => Cell(h.ToString()))));
            foreach (var r in rows)
            {
                csv.AppendLine(string.Join(",", new object?[]
                {
                    r.Id, r.Title, r.Status, r.Priority, r.Category, r.Responsible, r.Association, r.Author, r.Address, r.Lat, r.Lng, r.CreatedAt
                }.Select(Cell)));
            }
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        }

        // The select lists and limits the form shows. acabId is the association the appeal belongs to.
        private async Task PopulateAsync(AppealFormViewModel form, int? acabId)
        {
            form.Lookups = await _lookups.GetAsync();
            form.AcabName = form.Lookups.Acabs.FirstOrDefault(a => a.Id == acabId)?.DisplayName();
            form.MaxPhotos = _files.MaxPhotos;
        }
    }
}
