using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using CityFix_backend.Data;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
    // The CRUD shared by simple reference lists (categories, recipients), which the explanatory note
    // allows to work through the database context directly. Subclasses say how to search, order,
    // count the appeals that use a row, and which fields an edit may change.
    // A row used by appeals cannot be deleted (the foreign keys are RESTRICT as well).
    public abstract class ReferenceListController<TEntity> : AppController where TEntity : class, new()
    {
        protected readonly ApplicationDbContext Context;

        protected ReferenceListController(ApplicationDbContext context, IUserService users, IStringLocalizer<SharedResource> localizer)
            : base(users, localizer)
        {
            Context = context;
        }

        protected abstract int IdOf(TEntity entity);

        protected abstract string NameOf(TEntity entity);

        protected abstract IQueryable<TEntity> Filter(IQueryable<TEntity> query, string pattern);

        protected abstract IQueryable<TEntity> Order(IQueryable<TEntity> query);

        // Appeals per row id, for the given ids.
        protected abstract Task<Dictionary<int, int>> CountAppealsAsync(IReadOnlyCollection<int> ids);

        // Copies the editable fields of a posted entity onto the stored one (never the id).
        protected abstract void CopyEditable(TEntity from, TEntity to);

        // GET: {controller}?q=
        public async Task<IActionResult> Index(string? q, int page = 1)
        {
            var query = Context.Set<TEntity>().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q))
            {
                query = Filter(query, Search.ContainsPattern(q));
            }
            var result = await Order(query).ToPagedResultAsync(page);
            var counts = await CountAppealsAsync(result.Items.Select(IdOf).ToList());
            return View(new TablePageViewModel<TEntity> { Page = result, Counts = counts, Q = q });
        }

        // GET: {controller}/Create
        public IActionResult Create() => View(new TEntity());

        // POST: {controller}/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TEntity posted)
        {
            if (!ModelState.IsValid)
            {
                return View(posted);
            }

            var entity = new TEntity();
            CopyEditable(posted, entity);
            Context.Add(entity);
            await Context.SaveChangesAsync();
            FlashSuccess(L["Common.Saved"]);
            return RedirectToAction(nameof(Index));
        }

        // GET: {controller}/Edit/5
        public async Task<IActionResult> Edit(int id) => View(await FindAsync(id));

        // POST: {controller}/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TEntity posted)
        {
            if (!ModelState.IsValid)
            {
                return View(posted);
            }

            CopyEditable(posted, await FindAsync(id));
            await Context.SaveChangesAsync();
            FlashSuccess(L["Common.Saved"]);
            return RedirectToAction(nameof(Index));
        }

        // GET: {controller}/Delete/5
        public async Task<IActionResult> Delete(int id) => View(await CheckDeleteAsync(id));

        // POST: {controller}/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var check = await CheckDeleteAsync(id);
            if (check.Blocked)
            {
                return RedirectToAction(nameof(Delete), new { id });
            }

            Context.Remove(await FindAsync(id));
            await Context.SaveChangesAsync();
            FlashSuccess(L["Common.Deleted"]);
            return RedirectToAction(nameof(Index));
        }

        private async Task<TEntity> FindAsync(int id) =>
            await Context.Set<TEntity>().FindAsync(id)
            ?? throw new NotFoundException($"{typeof(TEntity).Name} {id} does not exist.");

        private async Task<DeleteCheck> CheckDeleteAsync(int id)
        {
            var entity = await FindAsync(id);
            var appeals = (await CountAppealsAsync(new[] { id })).GetValueOrDefault(id);
            return new DeleteCheck(id, NameOf(entity), appeals, appeals > 0 ? "Common.HasAppealsText" : null);
        }
    }
}
