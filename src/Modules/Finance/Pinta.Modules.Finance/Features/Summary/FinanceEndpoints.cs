using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Finance.Contracts;
using Pinta.Modules.Finance.Data;
using Pinta.Modules.Finance.Domain;
using Pinta.Modules.Finance.Features.Payouts;
using Pinta.Modules.Finance.Resources;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;
using Pinta.SharedKernel.Text;
using Pinta.SharedKernel.Time;

namespace Pinta.Modules.Finance.Features.Summary;

/// <summary>A job of the month: closed (its confirmed version) or cancelled (the deposit kept, without VAT).</summary>
public sealed record FinanceJobLine(
    Guid JobId, string Code, JobStatus Status, DateTimeOffset At, long IncomeCents, long VatCents, long MaterialCents, long PainterPayCents,
    long MarginCents);

public sealed record CostDto(Guid Id, DateOnly Date, OperatingCostCategory Category, string Description, long AmountCents, uint Version);

public sealed record CategoryTotal(OperatingCostCategory Category, long AmountCents);

/// <summary>
/// A5.1: the month's figures. When <see cref="Closed"/>, the totals are the ones frozen at closing (A5.3); jobs and costs
/// are listed as they are. <see cref="PendingPayouts"/> is every payout still owed, of any month.
/// </summary>
public sealed record MonthSummary(
    string Month,
    bool Closed,
    DateTimeOffset? ClosedAt,
    bool CanClose,
    long IncomeCents,
    long VatCents,
    long MaterialCents,
    long PainterPayCents,
    long GrossMarginCents,
    decimal MarginPct,
    long CostsCents,
    long ProfitCents,
    long PerPartnerCents,
    IReadOnlyList<CategoryTotal> CostsByCategory,
    IReadOnlyList<FinanceJobLine> Jobs,
    IReadOnlyList<CostDto> Costs,
    IReadOnlyList<PayoutDto> PendingPayouts);

public sealed record CostRequest(DateOnly Date, OperatingCostCategory Category, string Description, long AmountCents, uint? Version);

public sealed record CloseMonthRequest(string Month);

internal sealed class CostRequestValidator : AbstractValidator<CostRequest>
{
    public CostRequestValidator()
    {
        RuleFor(r => r.Category).IsInEnum();
        RuleFor(r => r.Description).NotEmpty().MaximumLength(200);
        RuleFor(r => r.AmountCents).GreaterThan(0).LessThan(100_000_000);
    }
}

internal sealed class CloseMonthRequestValidator : AbstractValidator<CloseMonthRequest>
{
    public CloseMonthRequestValidator() => RuleFor(r => r.Month).NotEmpty().Matches(@"^\d{4}-\d{2}$");
}

/// <summary>The month's figures (docs/data-model.md, "Cifras del mes"): one calculation for the panel, the closing and the CSV.</summary>
internal sealed class MonthFigures(FinanceDbContext db, IFinanceJobs jobs)
{
    public sealed record Figures(
        IReadOnlyList<FinanceJobLine> Jobs, IReadOnlyList<OperatingCost> Costs, decimal Income, decimal Vat, decimal Material, decimal PainterPay,
        decimal CostsTotal, decimal GrossMargin, decimal Profit, decimal PerPartner);

    public async Task<Figures> ForAsync(DateOnly first, CancellationToken cancellationToken)
    {
        var (from, to) = Months.Bounds(first);
        var inPeriod = await jobs.InPeriodAsync(from, to, cancellationToken);
        var ids = inPeriod.Select(j => j.JobId).ToList();
        var payouts = await db.PainterPayouts.AsNoTracking().Where(p => ids.Contains(p.JobId)).ToDictionaryAsync(p => p.JobId, p => p.Amount, cancellationToken);

        var lines = new List<FinanceJobLine>();
        foreach (var j in inPeriod)
        {
            decimal income, vat, material = 0, painter = 0;
            if (j.Status == JobStatus.Closed)
            {
                income = Money.FromCents(j.SubtotalCents);
                vat = Money.FromCents(j.VatCents);
                material = Money.FromCents(j.MaterialCents);
                // What the painter is actually paid, when the payout exists.
                painter = payouts.TryGetValue(j.JobId, out var paid) ? paid : Money.FromCents(j.PainterPayCents);
            }
            else
            {
                // The deposit kept, without VAT: in the proportion of the confirmed version's subtotal to its total.
                var retained = Money.FromCents(j.RetainedDepositCents);
                income = j.TotalCents == 0 ? 0 : R2(retained * j.SubtotalCents / j.TotalCents);
                vat = retained - income;
            }

            lines.Add(new FinanceJobLine(
                j.JobId, j.Code, j.Status, j.At, Money.ToCents(income), Money.ToCents(vat), Money.ToCents(material), Money.ToCents(painter),
                Money.ToCents(income - material - painter)));
        }

        var costs = await db.OperatingCosts.AsNoTracking()
            .Where(c => c.Date >= first && c.Date < first.AddMonths(1))
            .OrderBy(c => c.Date).ThenBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
        var incomeTotal = lines.Sum(l => Money.FromCents(l.IncomeCents));
        var materialTotal = lines.Sum(l => Money.FromCents(l.MaterialCents));
        var painterTotal = lines.Sum(l => Money.FromCents(l.PainterPayCents));
        var costsTotal = costs.Sum(c => c.Amount);
        var margin = incomeTotal - materialTotal - painterTotal;
        var profit = margin - costsTotal;
        return new Figures(
            lines, costs, incomeTotal, lines.Sum(l => Money.FromCents(l.VatCents)), materialTotal, painterTotal, costsTotal, margin, profit,
            R2(profit / 2));
    }

    private static decimal R2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Finanzas (A5.1–A5.3): the month, its operating costs and its closing.</summary>
internal static class FinanceEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var finance = app.MapGroup("/api/admin/finance").WithTags("Admin finance")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
        finance.MapGet("/summary", SummaryAsync).WithName("GetFinanceSummary");
        finance.MapGet("/costs", CostsAsync).WithName("ListOperatingCosts");
        finance.MapPost("/costs", AddCostAsync).WithValidation<CostRequest>().WithName("AddOperatingCost");
        finance.MapPut("/costs/{id:guid}", UpdateCostAsync).WithValidation<CostRequest>().WithName("UpdateOperatingCost");
        finance.MapDelete("/costs/{id:guid}", DeleteCostAsync).WithName("DeleteOperatingCost");
        finance.MapGet("/export", ExportAsync).WithName("ExportFinance").Produces<string>(StatusCodes.Status200OK, "text/csv");
        finance.MapPost("/closings", CloseAsync).WithValidation<CloseMonthRequest>().WithName("CloseMonth");
        finance.ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<Results<Ok<MonthSummary>, ProblemHttpResult>> SummaryAsync(
        string? month, FinanceDbContext db, MonthFigures figures, TimeProvider clock, CancellationToken cancellationToken) =>
        Months.TryParse(month, clock, out var first)
            ? TypedResults.Ok(await BuildAsync(first, db, figures, clock, cancellationToken))
            : MonthProblem();

    /// <summary>GET /api/admin/finance/export: the month's jobs, its costs and the totals (frozen ones when closed), as CSV.</summary>
    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> ExportAsync(
        string? month, FinanceDbContext db, MonthFigures figures, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (!Months.TryParse(month, clock, out var first))
        {
            return MonthProblem();
        }

        var m = await BuildAsync(first, db, figures, clock, cancellationToken);
        static string Eur(long cents) => (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        static string Snake(Enum value) => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
        var csv = new StringBuilder("kind,date,code,category,description,income_eur,vat_eur,material_eur,painter_eur,margin_eur,cost_eur,profit_eur,per_partner_eur\n");
        foreach (var j in m.Jobs)
        {
            csv.AppendJoin(',', j.Status == JobStatus.Closed ? "closed_job" : "cancelled_job", Lisbon(j.At).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Csv.Field(j.Code), "", "", Eur(j.IncomeCents), Eur(j.VatCents), Eur(j.MaterialCents), Eur(j.PainterPayCents), Eur(j.MarginCents), "", "", "");
            csv.Append('\n');
        }

        foreach (var c in m.Costs)
        {
            csv.AppendJoin(',', "cost", c.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "", Snake(c.Category), Csv.Field(c.Description),
                "", "", "", "", "", Eur(c.AmountCents), "", "");
            csv.Append('\n');
        }

        csv.AppendJoin(',', m.Closed ? "total_closed" : "total", m.Month, "", "", "", Eur(m.IncomeCents), Eur(m.VatCents), Eur(m.MaterialCents),
            Eur(m.PainterPayCents), Eur(m.GrossMarginCents), Eur(m.CostsCents), Eur(m.ProfitCents), Eur(m.PerPartnerCents));
        csv.Append('\n');
        return TypedResults.File([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())], "text/csv", $"finanzas-{m.Month}.csv");
    }

    private static DateTime Lisbon(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, BusinessDays.Lisbon).DateTime;

    private static async Task<MonthSummary> BuildAsync(
        DateOnly first, FinanceDbContext db, MonthFigures figures, TimeProvider clock, CancellationToken cancellationToken)
    {
        var f = await figures.ForAsync(first, cancellationToken);
        var closing = await db.MonthlyClosings.AsNoTracking().SingleOrDefaultAsync(c => c.Period == first, cancellationToken);
        var pending = await db.PainterPayouts.AsNoTracking().Where(p => p.Status == PayoutStatus.Pending).OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
        var (income, vat, material, painter, margin, costs, profit, perPartner) = closing is null
            ? (f.Income, f.Vat, f.Material, f.PainterPay, f.GrossMargin, f.CostsTotal, f.Profit, f.PerPartner)
            : (closing.Income, closing.Vat, closing.Material, closing.PainterPay, closing.GrossMargin, closing.Costs, closing.Profit, closing.PerPartner);
        return new MonthSummary(
            Months.Format(first),
            closing is not null,
            closing?.ClosedAt,
            closing is null && first < Months.Current(clock),
            Money.ToCents(income),
            Money.ToCents(vat),
            Money.ToCents(material),
            Money.ToCents(painter),
            Money.ToCents(margin),
            income == 0 ? 0 : Math.Round(margin / income * 100, 1, MidpointRounding.AwayFromZero),
            Money.ToCents(costs),
            Money.ToCents(profit),
            Money.ToCents(perPartner),
            [.. f.Costs.GroupBy(c => c.Category).OrderBy(g => g.Key).Select(g => new CategoryTotal(g.Key, Money.ToCents(g.Sum(c => c.Amount))))],
            f.Jobs,
            [.. f.Costs.Select(ToDto)],
            [.. pending.Select(PayoutEndpoints.ToDto)]);
    }

    private static async Task<Results<Ok<IReadOnlyList<CostDto>>, ProblemHttpResult>> CostsAsync(
        string? month, FinanceDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (!Months.TryParse(month, clock, out var first))
        {
            return MonthProblem();
        }

        return TypedResults.Ok<IReadOnlyList<CostDto>>([.. (await db.OperatingCosts.AsNoTracking()
            .Where(c => c.Date >= first && c.Date < first.AddMonths(1)).OrderBy(c => c.Date).ToListAsync(cancellationToken)).Select(ToDto)]);
    }

    /// <summary>A5.2: a cost of a day not in the future, in a month still open.</summary>
    private static async Task<Results<Ok<CostDto>, ProblemHttpResult>> AddCostAsync(
        CostRequest request, HttpContext http, FinanceDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (await RefuseAsync(db, clock, request.Date, cancellationToken) is { } problem)
        {
            return problem;
        }

        var cost = new OperatingCost
        {
            Date = request.Date,
            Category = request.Category,
            Description = request.Description.Trim(),
            Amount = Money.FromCents(request.AmountCents),
            CreatedAt = clock.GetUtcNow(),
            CreatedBy = http.User.GetUserId()!.Value,
        };
        db.OperatingCosts.Add(cost);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToDto(cost));
    }

    private static async Task<Results<Ok<CostDto>, ProblemHttpResult>> UpdateCostAsync(
        Guid id, CostRequest request, FinanceDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var cost = await db.OperatingCosts.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cost is null)
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        // Neither the month it was in nor the one it moves to may be closed.
        if ((await RefuseAsync(db, clock, cost.Date, cancellationToken) ?? await RefuseAsync(db, clock, request.Date, cancellationToken)) is { } problem)
        {
            return problem;
        }

        if (request.Version is { } version)
        {
            db.Entry(cost).Property(c => c.Version).OriginalValue = version;
        }

        cost.Date = request.Date;
        cost.Category = request.Category;
        cost.Description = request.Description.Trim();
        cost.Amount = Money.FromCents(request.AmountCents);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict);
        }

        return TypedResults.Ok(ToDto(cost));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteCostAsync(
        Guid id, FinanceDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var cost = await db.OperatingCosts.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cost is null)
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        if (await RefuseAsync(db, clock, cost.Date, cancellationToken) is { } problem)
        {
            return problem;
        }

        db.OperatingCosts.Remove(cost);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>A5.3: a finished month, once; its figures are frozen and its costs belong to the closing.</summary>
    private static async Task<Results<Ok<MonthSummary>, ProblemHttpResult>> CloseAsync(
        CloseMonthRequest request, HttpContext http, FinanceDbContext db, MonthFigures figures, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (!Months.TryParse(request.Month, clock, out var first) || first >= Months.Current(clock))
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["month"] = [FinanceMessages.Get("month_not_finished")] });
        }

        if (await db.MonthlyClosings.AnyAsync(c => c.Period == first, cancellationToken))
        {
            return ApiProblem.Create(StatusCodes.Status422UnprocessableEntity, ErrorCodes.PeriodClosed);
        }

        var f = await figures.ForAsync(first, cancellationToken);
        var closing = new MonthlyClosing
        {
            Period = first,
            Income = f.Income,
            Vat = f.Vat,
            Material = f.Material,
            PainterPay = f.PainterPay,
            GrossMargin = f.GrossMargin,
            Costs = f.CostsTotal,
            Profit = f.Profit,
            PerPartner = f.PerPartner,
            ClosedAt = clock.GetUtcNow(),
            ClosedBy = http.User.GetUserId()!.Value,
        };
        db.MonthlyClosings.Add(closing);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Both partners closed the same month at once: the unique period kept the first.
            return ApiProblem.Create(StatusCodes.Status422UnprocessableEntity, ErrorCodes.PeriodClosed);
        }

        // From now on the month's costs belong to the closing and are refused any change.
        await db.OperatingCosts.Where(c => c.Date >= first && c.Date < first.AddMonths(1))
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.ClosingId, closing.Id), cancellationToken);

        return await SummaryAsync(request.Month, db, figures, clock, cancellationToken);
    }

    /// <summary>A cost may not be dated in the future nor fall in a closed month (<c>period_closed</c>).</summary>
    private static async Task<ProblemHttpResult?> RefuseAsync(FinanceDbContext db, TimeProvider clock, DateOnly date, CancellationToken cancellationToken)
    {
        if (date > BusinessDays.Today(clock))
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["date"] = [FinanceMessages.Get("cost_in_future")] });
        }

        var month = Months.Of(date);
        return await db.MonthlyClosings.AnyAsync(c => c.Period == month, cancellationToken)
            ? ApiProblem.Create(StatusCodes.Status422UnprocessableEntity, ErrorCodes.PeriodClosed)
            : null;
    }

    private static ProblemHttpResult MonthProblem() => ApiProblem.Validation(new Dictionary<string, string[]> { ["month"] = ["Use YYYY-MM."] });

    private static CostDto ToDto(OperatingCost c) => new(c.Id, c.Date, c.Category, c.Description, Money.ToCents(c.Amount), c.Version);
}
