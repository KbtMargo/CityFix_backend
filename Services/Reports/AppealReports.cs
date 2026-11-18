using System.Globalization;
using ClosedXML.Excel;
using CityFix_backend.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace CityFix_backend.Services.Reports
{
    public static class AppealReports
    {
        private static DateTime Created(AppealExportRow row) =>
            DateTime.ParseExact(row.CreatedAt, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        public static byte[] Xlsx(IReadOnlyList<AppealExportRow> rows, ReportHeader header)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add(SharedText.Get("Report.Appeals.Sheet"));
            var firstRow = ExcelStyle.Header(sheet, header, columns: 12);

            var titles = new[]
            {
                "№", SharedText.Get("Field.Title"), SharedText.Get("Field.Status"), SharedText.Get("Field.Priority"),
                SharedText.Get("Field.Category"), SharedText.Get("Field.Responsible"), SharedText.Get("Field.Acab"),
                SharedText.Get("Field.Author"), SharedText.Get("Field.Address"), SharedText.Get("Field.Latitude"),
                SharedText.Get("Field.Longitude"), SharedText.Get("Field.CreatedAt")
            };
            for (var c = 0; c < titles.Length; c++) sheet.Cell(firstRow, c + 1).Value = titles[c];

            var r = firstRow + 1;
            foreach (var row in rows)
            {
                sheet.Cell(r, 1).Value = row.Id;
                sheet.Cell(r, 2).Value = row.Title;
                sheet.Cell(r, 3).Value = row.Status;
                sheet.Cell(r, 4).Value = row.Priority;
                sheet.Cell(r, 5).Value = row.Category;
                sheet.Cell(r, 6).Value = row.Responsible;
                sheet.Cell(r, 7).Value = row.Association ?? "";
                sheet.Cell(r, 8).Value = row.Author;
                sheet.Cell(r, 9).Value = row.Address ?? "";
                if (row.Lat != null) sheet.Cell(r, 10).Value = (double)row.Lat.Value;
                if (row.Lng != null) sheet.Cell(r, 11).Value = (double)row.Lng.Value;
                sheet.Cell(r, 12).Value = Created(row);
                r++;
            }

            var lastRow = Math.Max(r - 1, firstRow + 1);
            var table = sheet.Range(firstRow, 1, lastRow, titles.Length).CreateTable("Appeals");
            table.Theme = XLTableTheme.TableStyleLight9;
            table.ShowAutoFilter = true;

            sheet.Column(10).Style.NumberFormat.Format = "0.00000";
            sheet.Column(11).Style.NumberFormat.Format = "0.00000";
            sheet.Column(12).Style.DateFormat.Format = "dd.mm.yyyy hh:mm";

            var widths = new[] { 7, 44, 18, 18, 24, 30, 26, 22, 38, 11, 11, 17 };
            for (var c = 0; c < widths.Length; c++) sheet.Column(c + 1).Width = widths[c];
            sheet.Range(firstRow + 1, 1, lastRow, titles.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            foreach (var c in new[] { 2, 5, 6, 7, 8, 9 }) sheet.Column(c).Style.Alignment.WrapText = true;
            sheet.Range(firstRow, 1, firstRow, titles.Length).Style.Alignment.WrapText = true;

            sheet.SheetView.FreezeRows(firstRow);
            sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            sheet.PageSetup.FitToPages(1, 0);
            sheet.PageSetup.SetRowsToRepeatAtTop(firstRow, firstRow);

            return ExcelStyle.Save(workbook);
        }

        public static byte[] Pdf(IReadOnlyList<AppealExportRow> rows, ReportHeader header) =>
            Document.Create(document => document.Page(page =>
            {
                PdfStyle.Page(page, landscape: true);
                page.Header().Element(c => PdfStyle.Header(c, header));
                page.Footer().Element(PdfStyle.Footer);
                page.Content().Column(column =>
                {
                    column.Item().PaddingBottom(6).Text(SharedText.Format("Report.Appeals.Count", rows.Count)).FontColor(PdfStyle.Muted);
                    if (rows.Count == 0)
                    {
                        column.Item().PaddingTop(20).AlignCenter().Text(SharedText.Get("Empty.Filtered")).FontColor(PdfStyle.Muted);
                        return;
                    }
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(32);
                            c.RelativeColumn(3.2f);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(1.8f);
                            c.RelativeColumn(1.3f);
                            c.ConstantColumn(62);
                        });
                        table.Header(h =>
                        {
                            foreach (var title in new[]
                            {
                                "№", SharedText.Get("Field.Title"), SharedText.Get("Field.Status"), SharedText.Get("Field.Priority"),
                                SharedText.Get("Field.Category"), SharedText.Get("Field.Responsible"), SharedText.Get("Field.Author"),
                                SharedText.Get("Field.CreatedAt")
                            })
                            {
                                h.Cell().Element(PdfStyle.HeadCell).Text(title);
                            }
                        });

                        for (var i = 0; i < rows.Count; i++)
                        {
                            var row = rows[i];
                            var stripe = i % 2 == 1;
                            table.Cell().Element(c => PdfStyle.BodyCell(c, stripe)).Text(row.Id.ToString(CultureInfo.InvariantCulture)).FontColor(PdfStyle.Muted);
                            table.Cell().Element(c => PdfStyle.BodyCell(c, stripe)).Column(cell =>
                            {
                                cell.Item().Text(row.Title).SemiBold();
                                if (!string.IsNullOrWhiteSpace(row.Address))
                                {
                                    cell.Item().Text(row.Address).FontSize(8).FontColor(PdfStyle.Muted);
                                }
                            });
                            table.Cell().Element(c => PdfStyle.BodyCell(c, stripe)).Text(row.Status);
                            table.Cell().Element(c => PdfStyle.BodyCell(c, stripe)).Text(row.Priority);
                            table.Cell().Element(c => PdfStyle.BodyCell(c, stripe)).Text(row.Category);
                            table.Cell().Element(c => PdfStyle.BodyCell(c, stripe)).Text(row.Responsible);
                            table.Cell().Element(c => PdfStyle.BodyCell(c, stripe)).Text(row.Author);
                            table.Cell().Element(c => PdfStyle.BodyCell(c, stripe)).Text(Created(row).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
                        }
                    });
                });
            })).GeneratePdf();
    }

    internal static class ExcelStyle
    {
        public static int Header(IXLWorksheet sheet, ReportHeader header, int columns)
        {
            sheet.Style.Font.FontName = "Calibri";
            sheet.Style.Font.FontSize = 11;

            sheet.Cell(1, 1).Value = header.Title;
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontSize = 15;

            var lines = new[] { header.Scope, header.Filters, header.GeneratedLine }.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            for (var i = 0; i < lines.Count; i++)
            {
                var cell = sheet.Cell(i + 2, 1);
                cell.Value = lines[i];
                cell.Style.Font.FontColor = XLColor.FromHtml("#64748B");
                sheet.Range(i + 2, 1, i + 2, columns).Merge();
            }
            sheet.Range(1, 1, 1, columns).Merge();
            return lines.Count + 3;
        }

        public static byte[] Save(XLWorkbook workbook)
        {
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
