using System.ComponentModel.DataAnnotations;

namespace CityFix_backend.Models
{
    public enum AppealPriority
    {
        [Display(Name = "Enum.AppealPriority.Low")] Low,
        [Display(Name = "Enum.AppealPriority.Normal")] Normal,
        [Display(Name = "Enum.AppealPriority.Urgent")] Urgent
    }
}
