using System.Globalization;
using System.Resources;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Domain;
using Pinta.Modules.Painters.Contracts;
using Pinta.Modules.Quoting.Contracts;
using Pinta.SharedKernel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pinta.Modules.Jobs.Features.Protocols;

internal static class JobProtocolDocument
{
    private const string Ink = "#10201D";
    private const string Teal = "#0F766E";
    private const string Soft = "#E6F4F1";
    private const string Accent = "#F2B705";
    private const string Border = "#7C918B";
    private static readonly ResourceManager Texts = new("Pinta.Modules.Jobs.Resources.JobProtocol", typeof(JobProtocolDocument).Assembly);

    public static byte[] Render(Job job, DateOnly scheduledOn, UserSummary client, PainterSummary painter, AdminQuote quote)
    {
        var culture = CultureInfo.GetCultureInfo(client.Locale switch { Locale.Es => "es-ES", Locale.En => "en-GB", _ => "pt-PT" });
        string T(string key) => Texts.GetString(key, culture) ?? throw new InvalidOperationException($"Missing protocol resource {key}.");
        var numbers = client.Locale == Locale.En ? culture : CultureInfo.GetCultureInfo("es-ES");
        var address = string.Join(", ", new[] { job.AddressLine1, job.Floor, $"{job.PostalCode} {job.Locality}" }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        var total = $"{(quote.Breakdown.TotalCents / 100m).ToString("N2", numbers)} €";

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(text => text.FontSize(10).FontColor(Ink));
            page.Header().BorderBottom(2).BorderColor(Accent).PaddingBottom(12).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Text(text =>
                {
                    text.Span("pinta").FontSize(24).Bold().FontColor(Teal);
                    text.Span(" ●").FontSize(11).FontColor(Accent);
                });
                row.RelativeItem(3).AlignRight().Column(column =>
                {
                    column.Item().Text(T("Title")).FontSize(18).Bold();
                    column.Item().AlignRight().Text(job.Code).FontColor(Teal).Bold();
                });
            });
            page.Content().PaddingVertical(16).Column(body =>
            {
                body.Spacing(10);
                body.Item().Text(T("Agreement")).FontSize(14).Bold().FontColor(Teal);
                body.Item().Text(T("Before"));
                body.Item().Background(Soft).Padding(11).Column(details =>
                {
                    details.Spacing(6);
                    details.Item().Row(row =>
                    {
                        row.RelativeItem().PaddingRight(8).Element(c => Field(c, T("Client"), client.Name));
                        row.RelativeItem().Element(c => Field(c, T("Painter"), painter.Name));
                    });
                    details.Item().Row(row =>
                    {
                        row.RelativeItem(2).PaddingRight(8).Element(c => Field(c, T("Address"), address));
                        row.RelativeItem().Element(c => Field(c, T("Scheduled"), scheduledOn.ToString("d", culture)));
                    });
                });
                body.Item().Text(T("Scope")).Bold();
                var scope = string.Format(culture, T("WorkSummary"), quote.Elements.Count, quote.Breakdown.AreaM2, quote.Number);
                body.Item().Text(scope);
                var names = quote.Elements.Select(element => element.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();
                if (names.Length > 0)
                {
                    body.Item().Text(string.Join(" · ", names));
                }
                foreach (var extra in quote.Extras)
                {
                    body.Item().PaddingLeft(8).Text($"• {extra.Description}");
                }
                body.Item().AlignRight().Text($"{T("ApprovedTotal")}  {total}").Bold().FontColor(Teal);
                body.Item().Text(T("Payment")).FontSize(9);
                body.Item().ShowEntire().Element(c => Signatures(c, T));

                body.Item().PaddingTop(6).LineHorizontal(1).LineColor(Border);
                body.Item().Text(T("Handover")).FontSize(14).Bold().FontColor(Teal);
                body.Item().Text(T("After"));
                foreach (var key in new[] { "Finish", "Edges", "Protection", "Primer", "Cleaning", "ScopeCheck" })
                {
                    body.Item().Row(row =>
                    {
                        row.ConstantItem(18).AlignMiddle().Width(9).Height(9).Border(1).BorderColor(Border);
                        row.RelativeItem().Text(T(key));
                    });
                }
                body.Item().ShowEntire().Column(notes =>
                {
                    notes.Spacing(9);
                    notes.Item().Text(T("Notes")).Bold();
                    notes.Item().Height(36).Border(1).BorderColor(Border);
                    notes.Item().Element(c => Signatures(c, T));
                });
            });
            page.Footer().AlignRight().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Teal));
                text.Span(job.Code + "  ·  ");
                text.CurrentPageNumber();
                text.Span("/");
                text.TotalPages();
            });
        })).GeneratePdf();
    }

    private static void Field(IContainer container, string label, string value) => container.Column(column =>
    {
        column.Item().Text(label).FontSize(9).Bold().FontColor(Teal);
        column.Item().Text(value);
    });

    private static void Signatures(IContainer container, Func<string, string> text) => container.PaddingTop(21).Row(row =>
    {
        row.Spacing(16);
        row.ConstantItem(90).Element(c => Line(c, text("Date")));
        row.RelativeItem().Element(c => Line(c, text("ClientSignature")));
        row.RelativeItem().Element(c => Line(c, text("PainterSignature")));
    });

    private static void Line(IContainer container, string label) =>
        container.BorderTop(1).BorderColor(Border).PaddingTop(4).Text(label).FontSize(9);
}
