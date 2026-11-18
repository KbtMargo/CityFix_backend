using System.Globalization;
using CityFix_backend.Models;

namespace CityFix_backend.Helpers
{
    public static class Localized
    {
        public static string Language => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : "uk";

        public static bool IsEnglish => Language == "en";

        public static string Pick(string uk, string? en) =>
            IsEnglish ? (string.IsNullOrWhiteSpace(en) ? uk : en) : (string.IsNullOrWhiteSpace(uk) ? en ?? "" : uk);

        public static string DisplayName(this Category category) => Pick(category.Name, category.NameEn);

        public static string DisplayName(this Recipient recipient) => Pick(recipient.Name, recipient.NameEn);

        public static bool ShowsTranslation(this Appeal appeal) =>
            appeal.Language != Language && !string.IsNullOrWhiteSpace(appeal.TitleTranslated);

        public static string DisplayTitle(this Appeal appeal) =>
            appeal.ShowsTranslation() ? appeal.TitleTranslated! : appeal.Title;

        public static string? DisplayDescription(this Appeal appeal) =>
            appeal.ShowsTranslation() && appeal.Description != null ? appeal.DescriptionTranslated ?? appeal.Description : appeal.Description;

        private static string Show(string original, string? translated) =>
            !string.IsNullOrWhiteSpace(translated) && Services.TextLanguage.Detect(original) != Language ? translated : original;

        public static string DisplayName(this User user) => Show(user.FullName, user.FullNameTranslated);

        public static string? DisplayAddress(this Appeal appeal) =>
            appeal.Address == null ? null : Show(appeal.Address, appeal.AddressTranslated);

        public static string DisplayName(this Acab acab) => Pick(acab.Name, acab.NameEn);

        public static string? DisplayAddress(this Acab acab) =>
            acab.Address == null ? acab.AddressEn : Pick(acab.Address, acab.AddressEn);

        public static string? DisplayAssignedTo(this AppealUpdate update) =>
            update.AssignedTo == null ? null : Pick(update.AssignedTo, update.AssignedToEn);
    }
}
