using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using CityFix_backend.Data;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
    public abstract class ReferenceListController<TEntity> : AppController where TEntity : class, new()
    {
        protected readonly ApplicationDbContext Context;
        private readonly IAuditLog _audit;

        protected ReferenceListController(ApplicationDbContext context, IUserService users, IStringLocalizer<SharedResource> localizer, IAuditLog audit)
            : base(users, localizer)
        {
            Context = context;
            _audit = audit;
        }

        private Task AuditAsync(string verb, TEntity entity) =>
            AuditAsync(verb, IdOf(entity), NameOf(entity));

        private async Task AuditAsync(string verb, int id, string name) =>
            await _audit.RecordAsync(await GetActorAsync(), $"{typeof(TEntity).Name.ToLowerInvariant()}.{verb}", $"{typeof(TEntity).Name} #{id} {name}");

        protected abstract int IdOf(TEntity entity);

        protected abstract string NameOf(TEntity entity);

        protected abstract IQueryable<TEntity> Filter(IQueryable<TEntity> query, string pattern);

        protected abstract IQueryable<TEntity> Order(IQueryable<TEntity> query);

        protected abstract Task<Dictionary<int, int>> CountAppealsAsync(IReadOnlyCollection<int> ids);

        protected abstract void CopyEditable(TEntity from, TEntity to);

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

        public IActionResult Create() => View(new TEntity());

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
            await AuditAsync("create", entity);
            FlashSuccess(L["Common.Saved"]);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id) => View(await FindAsync(id));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TEntity posted)
        {
            if (!ModelState.IsValid)
            {
                return View(posted);
            }

            var entity = await FindAsync(id);
            CopyEditable(posted, entity);
            await Context.SaveChangesAsync();
            await AuditAsync("update", entity);
            FlashSuccess(L["Common.Saved"]);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id) => View(await CheckDeleteAsync(id));

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var check = await CheckDeleteAsync(id);
            if (check.Blocked)
            {
                return RedirectToAction(nameof(Delete), new { id });
            }

            var removed = await FindAsync(id);
            var name = NameOf(removed);
            Context.Remove(removed);
            await Context.SaveChangesAsync();
            await AuditAsync("delete", id, name);
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
