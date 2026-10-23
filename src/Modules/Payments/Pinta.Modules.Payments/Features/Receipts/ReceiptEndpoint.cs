using System.Globalization;
using System.Resources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Contracts;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Payments.Contracts;
using Pinta.Modules.Payments.Data;
using Pinta.Modules.Payments.Domain;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;
using Pinta.SharedKernel.Time;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pinta.Modules.Payments.Features.Receipts;

/// <summary>What a receipt shows; built from stored data, so the PDF is the same every time it is downloaded.</summary>
public sealed record ReceiptData(
    CompanyDetails Company,
    string Number,
    DateTimeOffset PaidAt,
    PaymentType Type,
    PaymentMethod? Method,
    long AmountCents,
    long RefundedCents,
    decimal VatPct,
    string JobCode,
    string ClientName,
    string? ClientTaxId,
    string? ClientCompany,
    string Address,
    Locale Locale);

/// <summary>
/// GET /api/jobs/{id}/payments/{paymentId}/receipt — the simulated receipt of a paid payment (C5.3), for the job's client
/// and admins; anyone else gets 404.
/// </summary>
internal static class ReceiptEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/jobs/{id:guid}/payments/{paymentId:guid}/receipt", HandleAsync)
            .WithTags("Payments")
            .RequireAuthorization()
            .WithName("GetPaymentReceipt")
            .Produces<byte[]>(StatusCodes.Status200OK, "application/pdf")
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> HandleAsync(
        Guid id, Guid paymentId, HttpContext http, PaymentsDbContext db, IPayableJobs jobs, IUserDirectory users, ICompanyDetails company,
        IPriceSnapshotProvider snapshots, CancellationToken cancellationToken)
    {
        var job = await jobs.FindAsync(id, cancellationToken);
        var payment = await db.Payments.AsNoTracking().Include(p => p.Refunds)
            .SingleOrDefaultAsync(p => p.Id == paymentId && p.JobId == id && p.PaidAt != null && p.ReceiptNumber != null, cancellationToken);
        var client = job is null ? null : await users.FindAsync(job.ClientId, cancellationToken);
        if (job is null || payment is null || client is null || !(http.User.IsInRole(RoleNames.Admin) || job.ClientId == http.User.GetUserId()))
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        var data = new ReceiptData(
            await company.GetAsync(cancellationToken), payment.ReceiptNumber!, payment.PaidAt!.Value, payment.Type, payment.Method,
            Money.ToCents(payment.Amount), Money.ToCents(payment.Refunded), (await snapshots.GetCurrentAsync(cancellationToken)).Rules.VatPct,
            job.Code, client.Name, job.TaxId, job.CompanyName, $"{job.AddressLine1}, {job.PostalCode} {job.Locality}", client.Locale);
        return TypedResults.File(ReceiptDocument.Render(data), "application/pdf", $"{payment.ReceiptNumber}.pdf");
    }
}

/// <summary>The receipt PDF (QuestPDF, Community licence), in the client's language.</summary>
public static class ReceiptDocument
{
    private static readonly ResourceManager Texts = new("Pinta.Modules.Payments.Resources.ReceiptTexts", typeof(ReceiptDocument).Assembly);

    public static byte[] Render(ReceiptData data)
    {
        var culture = CultureInfo.GetCultureInfo(data.Locale switch { Locale.Es => "es-ES", Locale.En => "en-GB", _ => "pt-PT" });
        string T(string key) => Texts.GetString(key, culture) ?? key;
        // Euros as in the web app: "1.270,00 €" in pt and es (docs/integrations.md).
        var numbers = CultureInfo.GetCultureInfo(data.Locale == Locale.En ? "en-GB" : "es-ES");
        string Euros(long cents) => $"{(cents / 100m).ToString("N2", numbers)} €";
        var baseCents = Money.ToCents(Math.Round(data.AmountCents / 100m / (1 + data.VatPct / 100m), 2, MidpointRounding.AwayFromZero));
        var paidAt = TimeZoneInfo.ConvertTime(data.PaidAt, BusinessDays.Lisbon);
        var kind = data.Type switch { PaymentType.Deposit => "deposit", PaymentType.Balance => "balance", _ => "site_visit" };
        var method = data.Method switch { PaymentMethod.Card => "card", PaymentMethod.MbWay => "mb_way", PaymentMethod.Multibanco => "multibanco", _ => null };

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(48);
            page.DefaultTextStyle(text => text.FontSize(10).FontColor("#10201D"));
            page.Header().Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("Pinta").FontSize(22).Bold().FontColor("#0F766E");
                    column.Item().Text(data.Company.Name);
                    column.Item().Text($"NIF {data.Company.TaxId}");
                    column.Item().Text(data.Company.Address);
                });
                row.ConstantItem(200).AlignRight().Column(column =>
                {
                    column.Item().Text(T("Title")).FontSize(18).Bold();
                    column.Item().Text($"{T("Number")}: {data.Number}");
                    column.Item().Text($"{T("Date")}: {paidAt.ToString("d MMMM yyyy", culture)}");
                    if (method is not null)
                    {
                        column.Item().Text($"{T("Method")}: {T($"Method.{method}")}");
                    }
                });
            });
            page.Content().PaddingVertical(24).Column(column =>
            {
                column.Spacing(16);
                column.Item().Background("#FFFBEB").Border(1).BorderColor("#F2B705").Padding(8)
                    .Text(T("Simulated")).Bold().FontColor("#6B5000");
                column.Item().Column(client =>
                {
                    client.Item().Text(T("Client")).Bold();
                    client.Item().Text(data.ClientCompany ?? data.ClientName);
                    if (data.ClientTaxId is not null)
                    {
                        client.Item().Text($"{T("TaxId")}: {data.ClientTaxId}");
                    }

                    client.Item().Text(data.Address);
                    client.Item().Text($"{T("Job")}: {data.JobCode}");
                });
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                    });
                    table.Cell().BorderBottom(1).BorderColor("#D7E3DF").PaddingBottom(4).Text(T("Description")).Bold();
                    table.Cell().BorderBottom(1).BorderColor("#D7E3DF").PaddingBottom(4).AlignRight().Text(T("Base")).Bold();
                    table.Cell().PaddingTop(6).Text($"{T($"Kind.{kind}")} · {data.JobCode}");
                    table.Cell().PaddingTop(6).AlignRight().Text(Euros(baseCents));
                    table.Cell().PaddingTop(4).Text(string.Format(culture, T("Vat"), data.VatPct.ToString("0.##", culture)));
                    table.Cell().PaddingTop(4).AlignRight().Text(Euros(data.AmountCents - baseCents));
                    table.Cell().BorderTop(1).BorderColor("#D7E3DF").PaddingTop(6).Text(T("Total")).Bold();
                    table.Cell().BorderTop(1).BorderColor("#D7E3DF").PaddingTop(6).AlignRight().Text(Euros(data.AmountCents)).Bold();
                    if (data.RefundedCents > 0)
                    {
                        table.Cell().PaddingTop(4).Text(T("Refunded"));
                        table.Cell().PaddingTop(4).AlignRight().Text($"− {Euros(data.RefundedCents)}");
                    }
                });
            });
            page.Footer().AlignCenter().Text(T("Simulated")).FontSize(8).FontColor("#4F5F5B");
        })).GeneratePdf();
    }
}
