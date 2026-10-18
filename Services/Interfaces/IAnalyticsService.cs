namespace CityFix_backend.Services.Interfaces
{
    // Whether a change is good, not which way it goes (KpiCard README).
    public enum DeltaKind { Good, Bad, Flat }

    public enum DeltaDirection { Up, Down, None }

    // One KpiCard: value, the change with its note, and a 7-point sparkline.
    public record Kpi(
        string Value,
        string? Unit,
        string DeltaText,
        DeltaDirection Direction,
        DeltaKind Kind,
        string Note,
        IReadOnlyList<double> Series);

    public record DashboardKpis(Kpi Active, Kpi ClosedThisMonth, Kpi Total, Kpi AvgResponse);

    public record ChartData(IReadOnlyList<string> Labels, IReadOnlyList<int> Values);

    public record AnalyticsData(
        int Total, int New, int InProgress, int Done, int Rejected,
        ChartData ByCategory, ChartData PerDay, ChartData ByStatus, ChartData ByPriority);

    // All figures are computed over the appeals the user may see.
    public interface IAnalyticsService
    {
        Task<DashboardKpis> GetDashboardKpisAsync(int userId);

        Task<AnalyticsData> GetAnalyticsAsync(int userId);
    }
}
