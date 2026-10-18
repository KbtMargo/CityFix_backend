using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public enum UserRole
    {
        [Display(Name = "Enum.UserRole.Resident")] Resident,
        [Display(Name = "Enum.UserRole.AcabAdmin")] AcabAdmin,
        [Display(Name = "Enum.UserRole.Administrator")] Administrator
    }
}
