using CityFix_backend.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CityFix_backend.Services.Reports
{
    public record ReportHeader(string Title, string Scope, string? Filters, string GeneratedBy, DateTime GeneratedAtUtc)
    {
        public string GeneratedLine =>
            SharedText.Format("Report.Generated", GeneratedAtUtc.ToDateTime(), GeneratedBy);

        public static string FileName(string kind, DateTime utc, string extension) =>
            $"cityfix-{kind}-{utc.ToKyiv():yyyyMMdd-HHmm}.{extension}";
    }

    internal static class PdfStyle
    {
        public static readonly string Ink = "#0F172A";
        public static readonly string Muted = "#64748B";
        public static readonly string Rule = "#E2E8F0";
        public static readonly string HeadFill = "#F1F5F9";
        public static readonly string Stripe = "#F8FAFC";
        public static readonly string Accent = "#2563EB";

        public static readonly string[] Fonts = { PickFont() };

        private static string PickFont()
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var candidates = new (string Family, string[] Files)[]
            {
                ("Segoe UI", new[] { Path.Combine(windows, "segoeui.ttf") }),
                ("Arial", new[] { Path.Combine(windows, "arial.ttf"), "/usr/share/fonts/truetype/msttcorefonts/Arial.ttf" }),
                ("DejaVu Sans", new[] { "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/dejavu/DejaVuSans.ttf" }),
                ("Liberation Sans", new[] { "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf" })
            };
            foreach (var (family, files) in candidates)
            {
                if (files.Any(File.Exists)) return family;
            }
            return "Arial";
        }

        public static void Page(PageDescriptor page, bool landscape)
        {
            page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink).FontFamily(Fonts));
        }

        public static void Header(IContainer container, ReportHeader header) =>
            container.PaddingBottom(12).BorderBottom(1).BorderColor(Rule).PaddingBottom(8).Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(header.Title).FontSize(16).SemiBold();
                    row.AutoItem().AlignBottom().Text("CityFix").FontSize(10).SemiBold().FontColor(Accent);
                });
                column.Item().PaddingTop(4).Text(header.Scope).FontSize(9).FontColor(Muted);
                if (!string.IsNullOrWhiteSpace(header.Filters))
                {
                    column.Item().Text(header.Filters).FontSize(9).FontColor(Muted);
                }
                column.Item().Text(header.GeneratedLine).FontSize(9).FontColor(Muted);
            });

        public static void Footer(IContainer container) =>
            container.BorderTop(1).BorderColor(Rule).PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("CityFix").FontSize(8).FontColor(Muted);
                row.AutoItem().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8).FontColor(Muted));
                    text.Span(SharedText.Get("Report.Page") + " ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });

        public static IContainer HeadCell(IContainer cell) =>
            cell.Background(HeadFill).BorderBottom(1).BorderColor(Rule).PaddingVertical(5).PaddingHorizontal(6)
                .DefaultTextStyle(x => x.SemiBold().FontSize(8).FontColor(Muted));

        public static IContainer BodyCell(IContainer cell, bool stripe) =>
            cell.Background(stripe ? Stripe : Colors.White).BorderBottom(0.5f).BorderColor(Rule)
                .PaddingVertical(5).PaddingHorizontal(6);
    }
}
