using CityFix_backend.Models;

namespace CityFix_backend.Services
{
    public class AppealFilter
    {
        public const string TabAll = "all";
        public const string TabNew = "new";
        public const string TabInProgress = "inprogress";
        public const string TabClosed = "closed";

        public const string ResponsibleAcab = "acab";

        public string? Q { get; set; }

        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        public int? CategoryId { get; set; }
        public string? Responsible { get; set; }
        public int? AcabId { get; set; }
        public AppealPriority? Priority { get; set; }
        public string? Tab { get; set; }
        public int Page { get; set; } = 1;

        public bool MineOnly { get; set; }

        public string CurrentTab => Tab is TabNew or TabInProgress or TabClosed ? Tab : TabAll;

        public bool ResponsibleIsAcab => Responsible == ResponsibleAcab;

        public int? ResponsibleRecipientId => int.TryParse(Responsible, out var id) ? id : null;

        public int ActiveFilterCount =>
            (From != null || To != null ? 1 : 0) + (CategoryId != null ? 1 : 0)
            + (ResponsibleIsAcab || ResponsibleRecipientId != null ? 1 : 0)
            + (AcabId != null ? 1 : 0) + (Priority != null ? 1 : 0);

        public Dictionary<string, string> ToRouteValues(bool includeTab = true, bool includePage = false)
        {
            var values = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(Q)) values["q"] = Q;
            if (From != null) values["from"] = From.Value.ToString("yyyy-MM-dd");
            if (To != null) values["to"] = To.Value.ToString("yyyy-MM-dd");
            if (CategoryId != null) values["categoryId"] = CategoryId.Value.ToString();
            if (ResponsibleIsAcab || ResponsibleRecipientId != null) values["responsible"] = Responsible!;
            if (AcabId != null) values["acabId"] = AcabId.Value.ToString();
            if (Priority != null) values["priority"] = Priority.Value.ToString();
            if (includeTab && CurrentTab != TabAll) values["tab"] = CurrentTab;
            if (includePage && Page > 1) values["page"] = Page.ToString();
            return values;
        }
    }
}
