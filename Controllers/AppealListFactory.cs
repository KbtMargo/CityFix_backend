using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
    // Builds the appeal list + detail modal model shared by Dashboard/Index and Appeals/Index.
    // An appealId the user may not open ends in 404 / 403 through ServiceExceptionFilter.
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
                Filter = filter,
                Lookups = await lookups.GetAsync(),
                Controller = controller,
                Actor = actor,
                Selected = selected
            };
        }
    }
}
