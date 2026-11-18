using System.IO.Compression;
using ClosedXML.Excel;
using CityFix_backend.Services;
using CityFix_backend.Services.Reports;

namespace CityFix_backend.Tests.Unit;

[Trait("Category", "Unit")]
public class ReportsTests
{
    static ReportsTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
    }

    private static readonly ReportHeader Header = new("Звернення", "Усі звернення", "Фільтри: «яма»", "Адмін", new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc));

    private static readonly AppealExportRow[] Rows = Enumerable.Range(1, 40).Select(i => new AppealExportRow(
        i, i == 1 ? "=HYPERLINK(\"http://evil\")" : $"Яма №{i}", "Нове", "Звичайний", "Дороги", "ОСББ «Затишок»", "ОСББ «Затишок»",
        "Олена Коваль", "вул. Проскурівська, 1", 49.42161m, 26.99653m, "2026-11-18 12:00")).ToArray();

    private static AnalyticsData Analytics => new(10, 2, 3, 4, 1,
        new ChartData(["Дороги", "Освітлення"], [6, 4]),
        new ChartData(Enumerable.Range(1, 30).Select(d => $"{d:00}.11").ToList(), Enumerable.Repeat(1, 30).ToList()),
        new ChartData(["Нове", "Виконано"], [2, 4]),
        new ChartData(["Низький", "Звичайний", "Терміновий"], [1, 8, 1]),
        ["new", "done"]);

    private static Kpi Kpi(string value) => new(value, null, "1", DeltaDirection.Up, DeltaKind.Good, "цього тижня", [1, 2, 3, 4, 5, 6, 7]);

    private static DashboardKpis Kpis => new(Kpi("5"), Kpi("4"), Kpi("10"), Kpi("1.5"));

    [Fact]
    public void Appeal_spreadsheet_has_the_header_and_one_row_per_appeal()
    {
        var bytes = AppealReports.Xlsx(Rows, Header);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheets.First();
        var text = string.Join("\n", sheet.CellsUsed().Select(c => c.GetString()));
        Assert.Contains("Звернення", text);
        Assert.Contains("Фільтри: «яма»", text);
        Assert.Contains("Яма №40", text);
        var titles = sheet.CellsUsed().Where(c => c.GetString().StartsWith("Яма №") || c.GetString().Contains("HYPERLINK")).ToList();
        Assert.Equal(40, titles.Count);
        Assert.DoesNotContain(sheet.CellsUsed(), c => c.HasFormula);
    }

    [Fact]
    public void Appeal_pdf_is_a_multi_page_document()
    {
        var pdf = AppealReports.Pdf(Rows, Header);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 2000);
    }

    [Fact]
    public void Empty_report_still_renders()
    {
        Assert.NotEmpty(AppealReports.Pdf([], Header));
        using var workbook = new XLWorkbook(new MemoryStream(AppealReports.Xlsx([], Header)));
        Assert.NotEmpty(workbook.Worksheets);
    }

    [Fact]
    public void Analytics_reports_render_in_both_formats()
    {
        var xlsx = AnalyticsReports.Xlsx(Analytics, Kpis, Header);
        using (var zip = new ZipArchive(new MemoryStream(xlsx))) Assert.Contains(zip.Entries, e => e.FullName.StartsWith("xl/worksheets/"));
        using var workbook = new XLWorkbook(new MemoryStream(xlsx));
        Assert.Contains("Освітлення", string.Join(" ", workbook.Worksheets.SelectMany(s => s.CellsUsed()).Select(c => c.GetString())));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(AnalyticsReports.Pdf(Analytics, Kpis, Header), 0, 4));
    }

    [Fact]
    public void File_names_use_kyiv_time()
    {
        Assert.Equal("cityfix-appeals-20261118-1200.xlsx", ReportHeader.FileName("appeals", Header.GeneratedAtUtc, "xlsx"));
    }
}
