using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public enum AppealStatus
    {
        [Display(Name = "Enum.AppealStatus.New")] New,
        [Display(Name = "Enum.AppealStatus.InProgress")] InProgress,
        [Display(Name = "Enum.AppealStatus.Done")] Done,
        [Display(Name = "Enum.AppealStatus.Rejected")] Rejected
    }
}
