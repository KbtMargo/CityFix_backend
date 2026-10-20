using System.Globalization;
using CityFix_backend.Models;

namespace CityFix_backend.Helpers
{
    // Data stored in two languages (category and recipient names, appeal texts) shown in the current UI language.
    // Always falls back to whatever is there, so a missing translation never leaves an empty label.
    public static class Localized
    {
        public static string Language => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : "uk";

        public static bool IsEnglish => Language == "en";

        // uk is the stored default, en its English version.
        public static string Pick(string uk, string? en) =>
            IsEnglish ? (string.IsNullOrWhiteSpace(en) ? uk : en) : (string.IsNullOrWhiteSpace(uk) ? en ?? "" : uk);

        public static string DisplayName(this Category category) => Pick(category.Name, category.NameEn);

        public static string DisplayName(this Recipient recipient) => Pick(recipient.Name, recipient.NameEn);

        // The appeal's text in the reader's language: the machine translation when the author wrote in the other one.
        public static bool ShowsTranslation(this Appeal appeal) =>
            appeal.Language != Language && !string.IsNullOrWhiteSpace(appeal.TitleTranslated);

        public static string DisplayTitle(this Appeal appeal) =>
            appeal.ShowsTranslation() ? appeal.TitleTranslated! : appeal.Title;

        public static string? DisplayDescription(this Appeal appeal) =>
            appeal.ShowsTranslation() && appeal.Description != null ? appeal.DescriptionTranslated ?? appeal.Description : appeal.Description;

        // A text is shown translated when it is written in the other language and a translation exists.
        private static string Show(string original, string? translated) =>
            !string.IsNullOrWhiteSpace(translated) && Services.TextLanguage.Detect(original) != Language ? translated : original;

        // People's names: transliterated or spelled in Ukrainian by TranslationWorker.
        public static string DisplayName(this User user) => Show(user.FullName, user.FullNameTranslated);

        public static string? DisplayAddress(this Appeal appeal) =>
            appeal.Address == null ? null : Show(appeal.Address, appeal.AddressTranslated);

        public static string DisplayName(this Acab acab) => Pick(acab.Name, acab.NameEn);

        public static string? DisplayAddress(this Acab acab) =>
            acab.Address == null ? acab.AddressEn : Pick(acab.Address, acab.AddressEn);

        // History: the name of who the appeal was assigned to.
        public static string? DisplayAssignedTo(this AppealUpdate update) =>
            update.AssignedTo == null ? null : Pick(update.AssignedTo, update.AssignedToEn);
    }
}
