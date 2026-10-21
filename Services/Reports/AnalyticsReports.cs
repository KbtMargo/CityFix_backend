using System.Globalization;
using ClosedXML.Excel;
using CityFix_backend.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace CityFix_backend.Services.Reports
{
    public static class AnalyticsReports
    {
        private record Breakdown(string Title, string Column, ChartData Data);

        private static IEnumerable<(string Label, Kpi Kpi)> Kpis(DashboardKpis k) => new[]
        {
            (SharedText.Get("Kpi.Active"), k.Active),
            (SharedText.Get("Kpi.ClosedThisMonth"), k.ClosedThisMonth),
            (SharedText.Get("Kpi.Total"), k.Total),
            (SharedText.Get("Kpi.AvgResponse"), k.AvgResponse)
        };

        private static IEnumerable<(string Label, int Value)> Totals(AnalyticsData d) => new[]
        {
            (SharedText.Get("Analytics.Total"), d.Total),
            (SharedText.Get("Analytics.Waiting"), d.New),
            (SharedText.Get("Analytics.InProgress"), d.InProgress),
            (SharedText.Get("Analytics.Resolved"), d.Done),
            (SharedText.Get("Enum.AppealStatus.Rejected"), d.Rejected)
        };

        private static Breakdown[] Breakdowns(AnalyticsData d) => new[]
        {
            new Breakdown(SharedText.Get("Analytics.ByStatus"), SharedText.Get("Field.Status"), d.ByStatus),
            new Breakdown(SharedText.Get("Analytics.ByCategory"), SharedText.Get("Field.Category"), d.ByCategory),
            new Breakdown(SharedText.Get("Analytics.ByPriority"), SharedText.Get("Field.Priority"), d.ByPriority)
        };

        private static string Value(Kpi kpi) => kpi.Unit == null ? kpi.Value : $"{kpi.Value} {kpi.Unit}";

        private static string Change(Kpi kpi)
        {
            var arrow = kpi.Direction switch { DeltaDirection.Up => "▲ ", DeltaDirection.Down => "▼ ", _ => "" };
            return $"{arrow}{kpi.DeltaText} {kpi.Note}".Trim();
        }

        public static byte[] Xlsx(AnalyticsData data, DashboardKpis kpis, ReportHeader header)
        {
            using var workbook = new XLWorkbook();

            var summary = workbook.Worksheets.Add(SharedText.Get("Report.Analytics.Summary"));
            var row = ExcelStyle.Header(summary, header, columns: 4);
            row = Section(summary, row, SharedText.Get("Report.Analytics.KeyFigures"),
                new[] { SharedText.Get("Report.Metric"), SharedText.Get("Report.Value"), SharedText.Get("Report.Change") },
                Kpis(kpis).Select(k => new object[] { k.Label, Value(k.Kpi), Change(k.Kpi) }), "Kpis");
            Section(summary, row + 1, SharedText.Get("Report.Analytics.Totals"),
                new[] { SharedText.Get("Report.Metric"), SharedText.Get("Report.Count") },
                Totals(data).Select(t => new object[] { t.Label, t.Value }), "Totals");
            summary.Column(1).Width = 34;
            summary.Column(2).Width = 16;
            summary.Column(3).Width = 30;

            var index = 0;
            foreach (var b in Breakdowns(data))
            {
                var sheet = workbook.Worksheets.Add(SheetName(b.Title));
                var start = ExcelStyle.Header(sheet, header with { Title = b.Title }, columns: 3);
                var total = Math.Max(b.Data.Values.Sum(), 1);
                Section(sheet, start, null,
                    new[] { b.Column, SharedText.Get("Report.Count"), SharedText.Get("Report.Share") },
                    b.Data.Labels.Zip(b.Data.Values, (label, value) => new object[] { label, value, (double)value / total }),
                    $"Breakdown{++index}");
                sheet.Column(3).Style.NumberFormat.Format = "0.0%";
                sheet.Column(1).Width = 40;
                sheet.Column(2).Width = 14;
                sheet.Column(3).Width = 12;
                sheet.SheetView.FreezeRows(start);
            }

            var days = workbook.Worksheets.Add(SheetName(SharedText.Get("Analytics.PerDay")));
            var daysStart = ExcelStyle.Header(days, header with { Title = SharedText.Get("Analytics.PerDay") }, columns: 2);
            Section(days, daysStart, null,
                new[] { SharedText.Get("Report.Date"), SharedText.Get("Report.NewAppeals") },
                data.PerDay.Labels.Zip(data.PerDay.Values, (label, value) => new object[] { label, value }), "PerDay");
            days.Column(1).Width = 14;
            days.Column(2).Width = 20;
            days.SheetView.FreezeRows(daysStart);

            return ExcelStyle.Save(workbook);
        }

        private static int Section(IXLWorksheet sheet, int row, string? caption, string[] headers, IEnumerable<object[]> rows, string name)
        {
            if (caption != null)
            {
                sheet.Cell(row, 1).Value = caption;
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 1).Style.Font.FontSize = 12;
                row++;
            }
            var first = row;
            for (var c = 0; c < headers.Length; c++) sheet.Cell(row, c + 1).Value = headers[c];
            foreach (var values in rows)
            {
                row++;
                for (var c = 0; c < values.Length; c++)
                {
                    sheet.Cell(row, c + 1).Value = values[c] switch
                    {
                        int i => i,
                        double d => d,
                        _ => Convert.ToString(values[c], CultureInfo.CurrentCulture) ?? ""
                    };
                }
            }
            var table = sheet.Range(first, 1, Math.Max(row, first + 1), headers.Length).CreateTable(name);
            table.Theme = XLTableTheme.TableStyleLight9;
            table.ShowAutoFilter = false;
            return Math.Max(row, first + 1) + 2;
        }

        private static string SheetName(string title)
        {
            var clean = new string(title.Where(c => !":\\/?*[]".Contains(c)).ToArray());
            return clean.Length <= 31 ? clean : clean[..31];
        }

        public static byte[] Pdf(AnalyticsData data, DashboardKpis kpis, ReportHeader header) =>
            Document.Create(document => document.Page(page =>
            {
                PdfStyle.Page(page, landscape: false);
                page.Header().Element(c => PdfStyle.Header(c, header));
                page.Footer().Element(PdfStyle.Footer);
                page.Content().Column(column =>
                {
                    column.Spacing(16);

                    column.Item().Row(row =>
                    {
                        row.Spacing(8);
                        foreach (var (label, kpi) in Kpis(kpis))
                        {
                            row.RelativeItem().Border(0.75f).BorderColor(PdfStyle.Rule).Padding(8).Column(card =>
                            {
                                card.Item().Text(label).FontSize(8).FontColor(PdfStyle.Muted);
                                card.Item().PaddingTop(2).Text(Value(kpi)).FontSize(16).SemiBold();
                                card.Item().Text(Change(kpi)).FontSize(8).FontColor(kpi.Kind switch
                                {
                                    DeltaKind.Good => "#047857",
                                    DeltaKind.Bad => "#B91C1C",
                                    _ => PdfStyle.Muted
                                });
                            });
                        }
                    });

                    column.Item().Text(text =>
                    {
                        text.DefaultTextStyle(x => x.FontColor(PdfStyle.Muted));
                        var first = true;
                        foreach (var (label, value) in Totals(data))
                        {
                            if (!first) text.Span("   ·   ");
                            text.Span(label + ": ");
                            text.Span(value.ToString(CultureInfo.InvariantCulture)).SemiBold().FontColor(PdfStyle.Ink);
                            first = false;
                        }
                    });

                    column.Item().Element(c => DayChart(c, data.PerDay));

                    foreach (var b in Breakdowns(data))
                    {
                        column.Item().Element(c => BarTable(c, b));
                    }
                });
            })).GeneratePdf();

        private static void BarTable(IContainer container, Breakdown b) => container.ShowEntire().Column(column =>
        {
            column.Item().PaddingBottom(4).Text(b.Title).FontSize(11).SemiBold();
            var total = Math.Max(b.Data.Values.Sum(), 1);
            var max = Math.Max(b.Data.Values.DefaultIfEmpty(0).Max(), 1);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2.2f);
                    c.RelativeColumn(3);
                    c.ConstantColumn(50);
                    c.ConstantColumn(50);
                });
                table.Header(h =>
                {
                    h.Cell().Element(PdfStyle.HeadCell).Text(b.Column);
                    h.Cell().Element(PdfStyle.HeadCell).Text("");
                    h.Cell().Element(PdfStyle.HeadCell).AlignRight().Text(SharedText.Get("Report.Count"));
                    h.Cell().Element(PdfStyle.HeadCell).AlignRight().Text(SharedText.Get("Report.Share"));
                });
                for (var i = 0; i < b.Data.Labels.Count; i++)
                {
                    var value = b.Data.Values[i];
                    table.Cell().Element(c => PdfStyle.BodyCell(c, false)).Text(b.Data.Labels[i]);
                    table.Cell().Element(c => PdfStyle.BodyCell(c, false)).AlignMiddle().Height(8).Row(bar =>
                    {
                        if (value > 0) bar.RelativeItem(value).Background(PdfStyle.Accent);
                        if (max - value > 0) bar.RelativeItem(max - value);
                    });
                    table.Cell().Element(c => PdfStyle.BodyCell(c, false)).AlignRight().Text(value.ToString(CultureInfo.InvariantCulture)).SemiBold();
                    table.Cell().Element(c => PdfStyle.BodyCell(c, false)).AlignRight()
                        .Text(((double)value / total).ToString("P1", CultureInfo.CurrentUICulture)).FontColor(PdfStyle.Muted);
                }
            });
        });

        private static void DayChart(IContainer container, ChartData perDay) => container.ShowEntire().Column(column =>
        {
            column.Item().PaddingBottom(4).Text(SharedText.Get("Analytics.PerDay")).FontSize(11).SemiBold();
            var max = Math.Max(perDay.Values.DefaultIfEmpty(0).Max(), 1);
            column.Item().Height(90).BorderBottom(0.75f).BorderColor(PdfStyle.Rule).Row(row =>
            {
                row.Spacing(2);
                foreach (var value in perDay.Values)
                {
                    row.RelativeItem().AlignBottom().Column(bar =>
                    {
                        if (value > 0)
                        {
                            bar.Item().AlignCenter().Text(value.ToString(CultureInfo.InvariantCulture)).FontSize(6).FontColor(PdfStyle.Muted);
                            bar.Item().Height(70f * value / max).Background(PdfStyle.Accent);
                        }
                    });
                }
            });
            column.Item().PaddingTop(2).Row(row =>
            {
                row.Spacing(2);
                for (var i = 0; i < perDay.Labels.Count; i++)
                {
                    var label = i % 5 == 0 || i == perDay.Labels.Count - 1 ? perDay.Labels[i] : "";
                    row.RelativeItem().AlignCenter().Text(label).FontSize(6).FontColor(PdfStyle.Muted);
                }
            });
        });
    }
}
