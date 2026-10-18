using CityFix_backend.Models;
using CityFix_backend.Services;
using CityFix_backend.Services.Interfaces;
using CityFix_backend.ViewModels;

namespace CityFix_backend.Controllers
{
    // Builds the appeal list + detail modal model shared by Dashboard/Index and Appeals/Index.
    internal static class AppealListFactory
    {
        public static async Task<AppealListViewModel> BuildAsync(
            IAppealService appeals, AppealFilter filter, int? appealId, User user, string controller)
        {
            Appeal? selected = null;
            if (appealId != null)
            {
                try
                {
                    selected = await appeals.GetByIdAsync(appealId.Value, user.Id);
                }
                catch (UnauthorizedAccessException)
                {
                    // Treated like a missing appeal: the caller answers 404.
                }
            }

            return new AppealListViewModel
            {
                Page = await appeals.GetFilteredAsync(filter, user.Id),
                Filter = filter,
                Lookups = await appeals.GetLookupsAsync(),
                Controller = controller,
                IsStaff = user.Role != UserRole.Resident,
                IsAdministrator = user.Role == UserRole.Administrator,
                Selected = selected,
                CanEditSelected = selected != null
            };
        }
    }
}
