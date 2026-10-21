using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using CityFix_backend.Data;
using CityFix_backend.Helpers;
using CityFix_backend.Models;
using CityFix_backend.Services.Interfaces;

namespace CityFix_backend.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class RecipientsController : ReferenceListController<Recipient>
    {
        public RecipientsController(ApplicationDbContext context, IUserService users, IStringLocalizer<SharedResource> localizer)
            : base(context, users, localizer)
        {
        }

        protected override int IdOf(Recipient entity) => entity.Id;

        protected override string NameOf(Recipient entity) => entity.DisplayName();

        protected override IQueryable<Recipient> Filter(IQueryable<Recipient> query, string pattern) =>
            query.Where(r => EF.Functions.ILike(r.Name, pattern, "\\")
                || (r.NameEn != null && EF.Functions.ILike(r.NameEn, pattern, "\\"))
                || (r.ContactInfo != null && EF.Functions.ILike(r.ContactInfo, pattern, "\\")));

        protected override IQueryable<Recipient> Order(IQueryable<Recipient> query) => query.OrderBy(r => r.Name).ThenBy(r => r.Id);

        protected override Task<Dictionary<int, int>> CountAppealsAsync(IReadOnlyCollection<int> ids) =>
            Context.Appeals.Where(a => a.RecipientId != null && ids.Contains(a.RecipientId.Value))
                .GroupBy(a => a.RecipientId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

        protected override void CopyEditable(Recipient from, Recipient to)
        {
            to.Name = from.Name.Trim();
            // An empty English name is filled by machine translation (TranslationWorker).
            to.NameEn = string.IsNullOrWhiteSpace(from.NameEn) ? null : from.NameEn.Trim();
            to.ContactInfo = string.IsNullOrWhiteSpace(from.ContactInfo) ? null : from.ContactInfo.Trim();
        }
    }
}
