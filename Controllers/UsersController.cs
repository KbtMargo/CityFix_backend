using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using CityFix_backend.Data;
using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
    // Administrators manage every account; association heads see the members of their own association.
    // Passwords are never typed here: Register sets them, or Create generates a one-time temporary password.
    [Authorize(Roles = "Administrator,AcabAdmin")]
    public class UsersController : AppController
    {
        private const string ProfileFields = "FirstName,LastName,Email,Phone,Role,AcabId";

        private readonly ApplicationDbContext _context;
        private readonly IAuthService _auth;
        private readonly IStringLocalizer<SharedResource> _l;

        public UsersController(ApplicationDbContext context, IAuthService auth, IStringLocalizer<SharedResource> localizer)
        {
            _context = context;
            _auth = auth;
            _l = localizer;
        }

        // GET: Users
        public async Task<IActionResult> Index(int page = 1)
        {
            var query = _context.Users.AsNoTracking().Include(u => u.Acab).AsQueryable();
            if (!User.IsInRole(nameof(UserRole.Administrator)))
            {
                var me = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == CurrentUserId);
                query = me.AcabId == null ? query.Where(u => u.Id == me.Id) : query.Where(u => u.AcabId == me.AcabId);
            }

            var result = await query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id).ToPagedResultAsync(page);
            var ids = result.Items.Select(u => u.Id).ToList();

            // A user with appeals or status changes cannot be deleted (their history would go with them).
            var appealCounts = await _context.Appeals.Where(a => ids.Contains(a.AuthorId))
                .GroupBy(a => a.AuthorId).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            ViewData["HasHistory"] = await _context.AppealUpdates.Where(u => ids.Contains(u.ChangedById))
                .Select(u => u.ChangedById).Distinct().ToListAsync();

            return View(new TablePageViewModel<User> { Page = result, Counts = appealCounts });
        }

        // GET: Users/Create
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Create()
        {
            await PopulateAcabsAsync(null);
            return View(new User());
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Create([Bind(ProfileFields)] User user)
        {
            ModelState.Remove(nameof(Models.User.PasswordHash));
            user.Email = _auth.NormalizeEmail(user.Email ?? "");
            await ValidateEmailIsFreeAsync(user);

            if (ModelState.IsValid)
            {
                var temporaryPassword = _auth.GenerateTemporaryPassword();
                user.PasswordHash = _auth.HashPassword(temporaryPassword);
                user.CreatedAt = DateTime.UtcNow;
                user.UpdatedAt = user.CreatedAt;
                _context.Add(user);
                await _context.SaveChangesAsync();

                // Shown once, never stored in plain text.
                FlashSuccess(_l["Users.CreatedWithPassword", user.Email, temporaryPassword]);
                return RedirectToAction(nameof(Index));
            }

            await PopulateAcabsAsync(user.AcabId);
            return View(user);
        }

        // GET: Users/Edit/5
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            await PopulateAcabsAsync(user.AcabId);
            return View(user);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Edit(int id, [Bind("Id," + ProfileFields)] User user)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            ModelState.Remove(nameof(Models.User.PasswordHash));
            user.Email = _auth.NormalizeEmail(user.Email ?? "");
            await ValidateEmailIsFreeAsync(user);

            if (ModelState.IsValid)
            {
                var existing = await _context.Users.FindAsync(id);
                if (existing == null)
                {
                    return NotFound();
                }

                // Profile fields only: PasswordHash and CreatedAt keep their stored values.
                existing.FirstName = user.FirstName;
                existing.LastName = user.LastName;
                existing.Email = user.Email;
                existing.Phone = user.Phone;
                existing.Role = user.Role;
                existing.AcabId = user.AcabId;
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                FlashSuccess(_l["Common.Saved"]);
                return RedirectToAction(nameof(Index));
            }

            await PopulateAcabsAsync(user.AcabId);
            return View(user);
        }

        // GET: Users/Delete/5  (confirmation dialog; blocked while the user has appeals or history)
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound();
            }
            ViewData["AppealCount"] = await _context.Appeals.CountAsync(a => a.AuthorId == id);
            ViewData["Blocked"] = await IsDeleteBlockedAsync(id);
            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (await IsDeleteBlockedAsync(id))
            {
                return RedirectToAction(nameof(Delete), new { id });
            }

            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                FlashSuccess(_l["Common.Deleted"]);
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> IsDeleteBlockedAsync(int id) =>
            id == CurrentUserId
            || await _context.Appeals.AnyAsync(a => a.AuthorId == id)
            || await _context.AppealUpdates.AnyAsync(u => u.ChangedById == id);

        private async Task ValidateEmailIsFreeAsync(User user)
        {
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == user.Email && u.Id != user.Id))
            {
                ModelState.AddModelError(nameof(Models.User.Email), _l["Auth.EmailTaken"]);
            }
        }

        private async Task PopulateAcabsAsync(int? selectedId)
        {
            ViewData["AcabId"] = new SelectList(await _context.Acabs.OrderBy(a => a.Name).ToListAsync(), "Id", "Name", selectedId);
        }
    }
}
