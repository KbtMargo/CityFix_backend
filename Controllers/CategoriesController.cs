using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using CityFix_backend.Data;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;

namespace CityFix_backend.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class CategoriesController : ReferenceListController<Category>
    {
        public CategoriesController(ApplicationDbContext context, IUserService users, IStringLocalizer<SharedResource> localizer)
            : base(context, users, localizer)
        {
        }

        protected override int IdOf(Category entity) => entity.Id;

        protected override string NameOf(Category entity) => entity.Name;

        protected override IQueryable<Category> Filter(IQueryable<Category> query, string pattern) =>
            query.Where(c => EF.Functions.ILike(c.Name, pattern, "\\"));

        protected override IQueryable<Category> Order(IQueryable<Category> query) => query.OrderBy(c => c.Name).ThenBy(c => c.Id);

        protected override Task<Dictionary<int, int>> CountAppealsAsync(IReadOnlyCollection<int> ids) =>
            Context.Appeals.Where(a => ids.Contains(a.CategoryId))
                .GroupBy(a => a.CategoryId).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

        protected override void CopyEditable(Category from, Category to) => to.Name = from.Name.Trim();
    }
}
