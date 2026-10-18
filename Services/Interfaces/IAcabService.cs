using CityFix_backend.Models;

namespace CityFix_backend.Services.Interfaces
{
    // Condominium associations and their two invitation codes.
    public interface IAcabService
    {
        Task<PagedResult<AcabRow>> GetPageAsync(string? q, int page);

        Task<Acab> GetAsync(int id);

        // Both codes are generated, unique across all associations.
        Task<Acab> CreateAsync(AcabInput input);

        Task UpdateAsync(int id, AcabInput input);

        Task<string> RegenerateCodeAsync(int id, AcabCodeKind kind);

        // Blocked while users or appeals belong to it.
        Task<DeleteCheck> CheckDeleteAsync(int id);

        Task DeleteAsync(int id);

        // Registration: the association a code belongs to and the role it grants; null if no such code.
        Task<(Acab Acab, UserRole Role)?> FindByCodeAsync(string code);
    }
}
