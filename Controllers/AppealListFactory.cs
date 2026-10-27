using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
    internal static class AppealListFactory
    {
        public static async Task<AppealListViewModel> BuildAsync(
            IAppealService appeals, ILookupService lookups, AppealFilter filter, int? appealId, User actor, string controller)
        {
            var selected = appealId != null ? await appeals.GetDetailsAsync(appealId.Value, actor) : null;
            var page = await appeals.GetFilteredAsync(filter, actor);

            return new AppealListViewModel
            {
                Page = page.Page,
                PossibleDuplicates = page.PossibleDuplicates,
                Supported = page.Supported,
                Filter = filter,
                Lookups = await lookups.GetAsync(),
                Controller = controller,
                Actor = actor,
                Selected = selected
            };
        }
    }
}
