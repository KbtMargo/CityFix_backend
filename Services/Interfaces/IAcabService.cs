using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    public interface IAcabService
    {
        Task<PagedResult<AcabRow>> GetPageAsync(string? q, int page);

        Task<Acab> GetAsync(int id);

        Task<Acab> CreateAsync(AcabInput input);

        Task UpdateAsync(int id, AcabInput input);

        Task<string> RegenerateCodeAsync(int id, AcabCodeKind kind);

        Task<DeleteCheck> CheckDeleteAsync(int id);

        Task DeleteAsync(int id);

        Task<(Acab Acab, UserRole Role)?> FindByCodeAsync(string code);
    }
}
