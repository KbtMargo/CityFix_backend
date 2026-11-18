using System.ComponentModel.DataAnnotations;
using CityFix_backend.Models;
using CityFix_backend.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CityFix_backend.ViewModels
{
    public class AppealFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(100, MinimumLength = 5, ErrorMessage = "Validation.Length")]
        [Display(Name = "Field.Title")]
        public string Title { get; set; } = "";

        [StringLength(1000, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.Description")]
        public string? Description { get; set; }

        [Range(-90, 90, ErrorMessage = "Validation.Range")]
        [Display(Name = "Field.Latitude")]
        public decimal? Lat { get; set; }

        [Range(-180, 180, ErrorMessage = "Validation.Range")]
        [Display(Name = "Field.Longitude")]
        public decimal? Lng { get; set; }

        [StringLength(200, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.NameEn")]
        public string? NameEn { get; set; }

        [StringLength(300, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.Address")]
        public string? Address { get; set; }

        [StringLength(300, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.AddressEn")]
        public string? AddressEn { get; set; }

        [Required(ErrorMessage = "Validation.Choose")]
        [Display(Name = "Field.Category")]
        public int? CategoryId { get; set; }

        [Required(ErrorMessage = "Validation.Choose")]
        [Display(Name = "Field.Responsible")]
        public string? Responsible { get; set; }

        [BindNever, ValidateNever] public AppealLookups Lookups { get; set; } = null!;
        [BindNever, ValidateNever] public string? AcabName { get; set; }
        [BindNever, ValidateNever] public List<AppealFile> Files { get; set; } = new();
        [BindNever, ValidateNever] public int MaxPhotos { get; set; }
        [BindNever, ValidateNever] public int RemainingPhotos => Math.Max(0, MaxPhotos - Files.Count);

        public AppealInput ToInput()
        {
            var acab = Responsible == AppealFilter.ResponsibleAcab;
            return new AppealInput(Title, Description, Lat, Lng, Address, CategoryId ?? 0,
                acab ? ResponsibleKind.Association : ResponsibleKind.Recipient,
                acab ? null : int.TryParse(Responsible, out var id) ? id : null);
        }

        public static AppealFormViewModel From(Appeal appeal) => new()
        {
            Id = appeal.Id,
            Title = appeal.Title,
            Description = appeal.Description,
            Lat = appeal.Lat,
            Lng = appeal.Lng,
            Address = appeal.Address,
            CategoryId = appeal.CategoryId,
            Responsible = ResponsibleValue(appeal),
            Files = appeal.Files
        };

        public static string? ResponsibleValue(Appeal appeal) =>
            appeal.ResponsibleKind == ResponsibleKind.Association ? AppealFilter.ResponsibleAcab : appeal.RecipientId?.ToString();
    }

    public class UserFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(100, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.FirstName")]
        public string FirstName { get; set; } = "";

        [StringLength(100, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.LastName")]
        public string? LastName { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [EmailAddress(ErrorMessage = "Validation.Email")]
        [Display(Name = "Field.Email")]
        public string Email { get; set; } = "";

        [Phone(ErrorMessage = "Validation.Phone")]
        [Display(Name = "Field.Phone")]
        public string? Phone { get; set; }

        [Display(Name = "Field.Role")]
        public UserRole Role { get; set; } = UserRole.Resident;

        [Display(Name = "Field.Acab")]
        public int? AcabId { get; set; }

        [BindNever, ValidateNever] public bool IsBlocked { get; set; }
        [BindNever, ValidateNever] public IReadOnlyList<Acab> Acabs { get; set; } = Array.Empty<Acab>();

        public UserInput ToInput() => new(FirstName, LastName, Email, Phone, Role, AcabId);

        public static UserFormViewModel From(User user) => new()
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            AcabId = user.AcabId,
            IsBlocked = user.IsBlocked
        };
    }

    public class AcabFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(200, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.Name")]
        public string Name { get; set; } = "";

        [StringLength(300, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.Address")]
        public string? Address { get; set; }

        [StringLength(200, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.NameEn")]
        public string? NameEn { get; set; }

        [StringLength(300, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.AddressEn")]
        public string? AddressEn { get; set; }

        [BindNever, ValidateNever] public string? ResidentCode { get; set; }
        [BindNever, ValidateNever] public string? HeadCode { get; set; }

        public AcabInput ToInput() => new(Name, Address, NameEn, AddressEn);

        public static AcabFormViewModel From(Acab acab) => new()
        {
            Id = acab.Id,
            Name = acab.Name,
            Address = acab.Address,
            NameEn = acab.NameEn,
            AddressEn = acab.AddressEn,
            ResidentCode = acab.ResidentCode,
            HeadCode = acab.HeadCode
        };
    }

    public class ProfileFormViewModel
    {
        [Required(ErrorMessage = "Validation.Required")]
        [StringLength(100, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.FirstName")]
        public string FirstName { get; set; } = "";

        [StringLength(100, ErrorMessage = "Validation.MaxLength")]
        [Display(Name = "Field.LastName")]
        public string? LastName { get; set; }

        [Required(ErrorMessage = "Validation.Required")]
        [EmailAddress(ErrorMessage = "Validation.Email")]
        [Display(Name = "Field.Email")]
        public string Email { get; set; } = "";

        [Phone(ErrorMessage = "Validation.Phone")]
        [Display(Name = "Field.Phone")]
        public string? Phone { get; set; }

        public ProfileInput ToInput() => new(FirstName, LastName, Email, Phone);

        public static ProfileFormViewModel From(User user) => new()
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Phone = user.Phone
        };
    }
}
