using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public enum AppealStatus
    {
        [Display(Name = "Enum.AppealStatus.New")] New = 0,
        [Display(Name = "Enum.AppealStatus.InProgress")] InProgress = 1,
        [Display(Name = "Enum.AppealStatus.Done")] Done = 2,
        [Display(Name = "Enum.AppealStatus.Rejected")] Rejected = 3,
        [Display(Name = "Enum.AppealStatus.UnderReview")] UnderReview = 4,
        [Display(Name = "Enum.AppealStatus.NeedsInfo")] NeedsInfo = 5,
        [Display(Name = "Enum.AppealStatus.OnHold")] OnHold = 6,
        [Display(Name = "Enum.AppealStatus.Duplicate")] Duplicate = 7
    }
}
