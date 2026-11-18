using System.Globalization;
using System.Net;
using System.Text;
using CityFix_backend.Services.Interfaces;

namespace CityFix_backend.Helpers
{
    public class EmailTemplate
    {
        private const string Font = "font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif;";
        private const string Ink = "#0f172a";
        private const string Body = "#334155";
        private const string Muted = "#64748b";
        private const string Accent = "#155dfc";

        public required string Subject { get; init; }
        public string? Preheader { get; init; }
        public required string Heading { get; init; }
        public List<string> Paragraphs { get; init; } = new();
        public string? QuoteLabel { get; init; }
        public string? Quote { get; init; }
        public string? ButtonText { get; init; }
        public string? ButtonUrl { get; init; }
        public List<string> Notes { get; init; } = new();

        public static string Greeting(string? firstName) =>
            string.IsNullOrWhiteSpace(firstName) ? SharedText.Get("Email.GreetingNoName") : SharedText.Format("Email.Greeting", firstName);

        public EmailMessage Render() => new(Subject, RenderText(), RenderHtml());

        private bool HasButton => ButtonText != null && ButtonUrl != null;

        private string RenderText()
        {
            var text = new StringBuilder();
            text.AppendLine(Heading).AppendLine();
            foreach (var paragraph in Paragraphs)
            {
                text.AppendLine(paragraph).AppendLine();
            }
            if (Quote != null)
            {
                text.AppendLine(QuoteLabel == null ? $"«{Quote}»" : $"{QuoteLabel}: «{Quote}»").AppendLine();
            }
            if (HasButton)
            {
                text.AppendLine($"{ButtonText}: {ButtonUrl}").AppendLine();
            }
            foreach (var note in Notes)
            {
                text.AppendLine(note);
            }
            text.AppendLine().AppendLine("—")
                .AppendLine($"{SharedText.Get("App.Name")} · {SharedText.Get("App.Tagline")}")
                .AppendLine(SharedText.Get("Email.AutoReply"));
            return text.ToString();
        }

        private string RenderHtml()
        {
            static string E(string value) => WebUtility.HtmlEncode(value);
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var app = SharedText.Get("App.Name");

            var html = new StringBuilder();
            html.Append($"""
                <!DOCTYPE html>
                <html lang="{lang}">
                <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <meta name="color-scheme" content="light">
                <title>{E(Subject)}</title>
                </head>
                <body style="margin:0;padding:0;background:#f1f5f9;">
                """);
            if (Preheader != null)
            {
                html.Append($"""<div style="display:none;max-height:0;overflow:hidden;opacity:0;">{E(Preheader)}</div>""");
            }
            html.Append($"""
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f1f5f9;">
                <tr><td align="center" style="padding:32px 16px;">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:560px;">
                <tr><td style="padding:0 4px 20px;">
                  <table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>
                    <td width="32" height="32" align="center" style="width:32px;height:32px;border-radius:8px;background:#0c141f;color:#ffffff;{Font}font-size:17px;font-weight:700;line-height:32px;">{E(app[..1])}</td>
                    <td style="padding-left:10px;{Font}font-size:19px;font-weight:700;color:{Ink};">{E(app)}</td>
                  </tr></table>
                </td></tr>
                <tr><td style="background:#ffffff;border:1px solid #e2e8f0;border-radius:12px;padding:32px;">
                  <h1 style="margin:0 0 16px;{Font}font-size:22px;line-height:30px;font-weight:700;color:{Ink};">{E(Heading)}</h1>
                """);
            foreach (var paragraph in Paragraphs)
            {
                html.Append($"""<p style="margin:0 0 16px;{Font}font-size:15px;line-height:24px;color:{Body};">{E(paragraph)}</p>""");
            }
            if (Quote != null)
            {
                html.Append($"""<div style="margin:0 0 16px;padding:12px 16px;border-left:3px solid {Accent};border-radius:6px;background:#eff6ff;{Font}font-size:15px;line-height:24px;color:{Body};">""");
                if (QuoteLabel != null)
                {
                    html.Append($"""<div style="margin-bottom:4px;font-size:12px;line-height:16px;font-weight:600;text-transform:uppercase;letter-spacing:.04em;color:{Muted};">{E(QuoteLabel)}</div>""");
                }
                html.Append($"{E(Quote)}</div>");
            }
            if (HasButton)
            {
                html.Append($"""
                    <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:24px 0;"><tr>
                      <td style="border-radius:8px;background:{Accent};"><a href="{E(ButtonUrl!)}" target="_blank" style="display:inline-block;padding:12px 28px;border-radius:8px;{Font}font-size:15px;line-height:20px;font-weight:600;color:#ffffff;text-decoration:none;">{E(ButtonText!)}</a></td>
                    </tr></table>
                    """);
            }
            foreach (var note in Notes)
            {
                html.Append($"""<p style="margin:0 0 8px;{Font}font-size:13px;line-height:20px;color:{Muted};">{E(note)}</p>""");
            }
            if (HasButton)
            {
                html.Append($"""
                    <p style="margin:24px 0 0;padding-top:16px;border-top:1px solid #e2e8f0;{Font}font-size:12px;line-height:18px;color:{Muted};">
                      {E(SharedText.Get("Email.ButtonFallback"))}<br>
                      <a href="{E(ButtonUrl!)}" target="_blank" style="color:{Accent};word-break:break-all;">{E(ButtonUrl!)}</a>
                    </p>
                    """);
            }
            html.Append($"""
                </td></tr>
                <tr><td align="center" style="padding:20px 4px 0;{Font}font-size:12px;line-height:18px;color:{Muted};">
                  {E(app)} · {E(SharedText.Get("App.Tagline"))}<br>{E(SharedText.Get("Email.AutoReply"))}
                </td></tr>
                </table>
                </td></tr>
                </table>
                </body>
                </html>
                """);
            return html.ToString();
        }
    }
}
