using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Finance.Contracts;
using Pinta.Modules.Finance.Data;
using Pinta.Modules.Finance.Domain;
using Pinta.Modules.Finance.Features.Summary;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Identity.Domain;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Domain;
using Pinta.Modules.Painters.Data;
using Pinta.Modules.Painters.Domain;
using Pinta.Modules.Payments.Contracts;
using Pinta.Modules.Quoting.Contracts;
using Pinta.Pricing;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Time;

namespace Pinta.Api.Seed;

/// <summary>
/// The demo's clients and jobs (docs/plan.md, "Datos de demo"): last month closed for Finanzas (PT-…-0091 to 0093,
/// costs, payouts paid) and one job in every state this month (0101 to 0108, 0094 and 0095), with generated photos.
/// Dates are relative to today; amounts come from the pricing engine.
/// </summary>
internal static class DemoJobsSeeder
{
    private sealed record Client(string Name, string Email, string Phone, string? TaxId, string? Company);

    private static readonly Client Ana = new("Ana Costa", "ana@demo.pinta.pt", "+351912000101", null, null);
    private static readonly Client Miguel = new("Miguel Rocha", "miguel@demo.pinta.pt", "+351912000102", null, null);
    private static readonly Client LisboaRent = new("Lisboa Rent, Lda.", "gestao@demo.pinta.pt", "+351212000103", "509123457", "Lisboa Rent, Lda.");

    // The golden cases of docs/pricing-engine.md as the builder sends them.
    private static readonly QuoteElementRequest Facade = Surface("facade", "exterior", 12m, 6m);
    private static readonly QuoteElementRequest Wall = Surface("interior_wall", "matte", 3m, 2.6m);
    private static readonly QuoteElementRequest Bedroom = Room("bedroom", "matte", 4m, 3m);
    private static readonly QuoteElementRequest[] Flat =
    [
        Room("living", "matte", 5m, 4m),
        Room("kitchen", "satin", 3.5m, 3m),
        Room("bedroom", "matte", 4m, 3.5m),
        new(ElementKind.Room, null, "bedroom", RoomSize.M, null, null, null, [true, true, true, false], true, null, "matte", false),
        Room("bathroom", "anti_damp", 2.5m, 2m, damage: true),
        Room("hallway", "matte", 6m, 1.2m, ceiling: false),
    ];

    public static async Task SeedAsync(IServiceProvider services, UserManager<User> users, string password, DateTimeOffset now)
    {
        var clients = new Dictionary<Client, User>();
        foreach (var c in new[] { Ana, Miguel, LisboaRent })
        {
            var user = new User
            {
                UserName = c.Email, Email = c.Email, EmailConfirmed = true, PhoneNumber = c.Phone, Name = c.Name, Locale = Locale.Pt,
                TaxId = c.TaxId, CompanyName = c.Company, CreatedAt = now, TermsAcceptedAt = now,
            };
            Ensure(await users.CreateAsync(user, password));
            Ensure(await users.AddToRoleAsync(user, RoleNames.Client));
            clients[c] = user;
        }

        var nathan = (await users.FindByEmailAsync("nathan@demo.pinta.pt"))!;
        var painters = services.GetRequiredService<PaintersDbContext>();
        var rui = await painters.Painters.SingleAsync(p => p.Email == "rui@demo.pinta.pt");
        var today = BusinessDays.Today(services.GetRequiredService<TimeProvider>());
        var b = new DemoJobBuilder(services, nathan.Id, today.Year);

        await LastMonthAsync(services, b, clients, rui, nathan, today, now);
        await ThisMonthAsync(b, clients, rui, today);

        // New requests are numbered after the demo jobs.
        await services.GetRequiredService<JobsDbContext>().Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO jobs.code_counters (year, last) VALUES ({today.Year}, 120)
            ON CONFLICT (year) DO UPDATE SET last = GREATEST(code_counters.last, 120)
            """);
    }

    /// <summary>Last month, closed (H7 exit criterion): two jobs by Rui and a late cancellation, costs and payouts paid.</summary>
    private static async Task LastMonthAsync(
        IServiceProvider services, DemoJobBuilder b, Dictionary<Client, User> clients, Painter rui, User nathan, DateOnly today, DateTimeOffset now)
    {
        var month = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        DateTimeOffset On(int day, int hour = 10) => new(month.AddDays(day - 1).ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero);
        var lisboa = ("Lisboa", "Lisboa");

        foreach (var (number, client, address, occupancy, element, days) in new[]
                 {
                     (91, LisboaRent, ("Avenida da Liberdade, 120", "1250-146"), Occupancy.Empty, Facade, (submitted: 1, confirmed: 2, deposit: 3, start: 8, closed: 10, payout: 12)),
                     (92, Miguel, ("Rua Augusta, 50", "1100-053"), Occupancy.Empty, Wall, (submitted: 5, confirmed: 6, deposit: 6, start: 14, closed: 20, payout: 22)),
                 })
        {
            var job = await b.RequestAsync(number, clients[client], client.TaxId, client.Company, address, lisboa, occupancy, [element], On(days.submitted));
            var quote = await b.ConfirmAsync(job, On(days.confirmed));
            await b.PayAsync(job, clients[client].Id, PaymentType.Deposit, quote.Breakdown.DepositCents, On(days.deposit, 18), PaymentMethod.Card);
            job.TransitionTo(JobStatus.DepositPaid, JobActor.System, null, null, On(days.deposit, 18));
            await b.ScheduleAsync(job, rui, quote, DateOnly.FromDateTime(On(days.start).UtcDateTime), On(days.deposit, 20));
            job.TransitionTo(JobStatus.InProgress, JobActor.Painter, rui.UserId, null, On(days.start, 8));
            job.TransitionTo(JobStatus.Finished, JobActor.Painter, rui.UserId, null, On(days.start, 17));
            await b.CompleteAsync(job);
            await b.PayAsync(job, clients[client].Id, PaymentType.Balance, quote.Breakdown.BalanceCents, On(days.closed), PaymentMethod.Card);
            job.TransitionTo(JobStatus.Closed, JobActor.System, null, null, On(days.closed));
            await b.SaveAsync();
            await b.PayoutAsync(job, rui, quote, On(days.closed), On(days.payout), $"TRF-{today.Year}-{number:0000}");
        }

        // PT-…-0093: cancelled by Ana 3 days before the start; half the deposit refunded.
        var ana = await b.RequestAsync(93, clients[Ana], null, null, ("Rua de São Bento, 300", "1200-821"), lisboa, Occupancy.Inhabited, [Bedroom], On(3));
        var anaQuote = await b.ConfirmAsync(ana, On(4));
        var deposit = await b.PayAsync(ana, clients[Ana].Id, PaymentType.Deposit, anaQuote.Breakdown.DepositCents, On(5), PaymentMethod.Card);
        ana.TransitionTo(JobStatus.DepositPaid, JobActor.System, null, null, On(5));
        await b.ScheduleAsync(ana, rui, anaQuote, month.AddDays(14), On(5, 15));
        ana.TransitionTo(JobStatus.Cancelled, JobActor.Client, clients[Ana].Id, "Mudança de planos.", On(12));
        await b.WithdrawAsync(ana, On(12));
        await b.SaveAsync();
        await b.RefundHalfAsync(deposit, 93, On(12));

        // Operating costs: hosting, domain and mail, and Stripe's fees on the five payments (1,5% + 0,25 € each).
        var finance = services.GetRequiredService<FinanceDbContext>();
        var stripeFees = b.PaymentsMade.Sum(amount => Math.Round(amount * 0.015m, 2, MidpointRounding.AwayFromZero) + 0.25m);
        foreach (var (day, category, description, amount) in new[]
                 {
                     (1, OperatingCostCategory.Infrastructure, "Alojamento do servidor", 25.00m),
                     (1, OperatingCostCategory.Infrastructure, "Domínio e correio", 6.00m),
                     (28, OperatingCostCategory.PaymentFees, "Comissões Stripe do mês", stripeFees),
                 })
        {
            finance.OperatingCosts.Add(new OperatingCost
            {
                Date = month.AddDays(day - 1), Category = category, Description = description, Amount = amount, CreatedAt = On(day, 18), CreatedBy = nathan.Id,
            });
        }

        await finance.SaveChangesAsync();

        // The month is closed with its figures frozen, as the partners would on the 1st.
        var figures = await services.GetRequiredService<MonthFigures>().ForAsync(month, CancellationToken.None);
        var closing = new MonthlyClosing
        {
            Period = month, Income = figures.Income, Vat = figures.Vat, Material = figures.Material, PainterPay = figures.PainterPay,
            GrossMargin = figures.GrossMargin, Costs = figures.CostsTotal, Profit = figures.Profit, PerPartner = figures.PerPartner,
            ClosedAt = now, ClosedBy = nathan.Id,
        };
        finance.MonthlyClosings.Add(closing);
        await finance.SaveChangesAsync();
        await finance.OperatingCosts.Where(c => c.Date >= month && c.Date < month.AddMonths(1))
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.ClosingId, closing.Id));
    }

    /// <summary>One job in every state the demo shows (docs/plan.md, "Trabajos"), with D = today.</summary>
    private static async Task ThisMonthAsync(DemoJobBuilder b, Dictionary<Client, User> clients, Painter rui, DateOnly today)
    {
        DateTimeOffset D(int days, int hour = 10)
        {
            var local = today.AddDays(days).ToDateTime(new TimeOnly(hour, 0));
            return new DateTimeOffset(local, BusinessDays.Lisbon.GetUtcOffset(local)).ToUniversalTime();
        }

        var lisboa = ("Lisboa", "Lisboa");
        var oeiras = ("Oeiras", "Oeiras");

        // 0101 · Ana · TC01 in review since D−1 with 4 photos: to review live.
        var j101 = await b.RequestAsync(101, clients[Ana], null, null, ("Rua das Flores, 24", "1200-195"), lisboa, Occupancy.Empty,
            [Bedroom with { ClientKey = "room-1" }], D(-1), BusinessDays.Add(today, 2), "Quarto com uma mancha de humidade junto à janela.");
        await b.SaveAsync();
        await b.PhotosAsync(j101, clients[Ana].Id, Pinta.Modules.Media.Contracts.MediaContext.Element, ["request-1.png", "request-4.png"], "room-1", D(-1));
        await b.PhotosAsync(j101, clients[Ana].Id, Pinta.Modules.Media.Contracts.MediaContext.JobGeneral, ["request-2.png", "request-3.png"], null, D(-1));

        // 0102 · Miguel · TC09 in review since D−2, due today (the next working day at weekends): the amber light.
        await b.RequestAsync(102, clients[Miguel], null, null, ("Rua do Ouro, 210", "1100-062"), lisboa, Occupancy.Furnished, Flat, D(-2),
            BusinessDays.IsBusinessDay(today) ? today : BusinessDays.Add(today, 1));

        // 0103 · Lisboa Rent · TC11 (Premium) with a site visit by Rui at D+2.
        var j103 = await b.RequestAsync(103, clients[LisboaRent], LisboaRent.TaxId, LisboaRent.Company, ("Avenida da República, 15", "1050-185"), lisboa,
            Occupancy.Empty, [.. Enumerable.Range(1, 8).Select(_ => SizedRoom("bedroom", RoomSize.L)), SizedRoom("living", RoomSize.L)], D(-3), BusinessDays.Add(today, 0));
        j103.SiteVisitAt = D(2);
        j103.SiteVisitAssigneeId = rui.UserId;
        j103.SiteVisitPrice = 0;
        j103.RequiresSiteVisit = true;
        j103.TransitionTo(JobStatus.SiteVisit, JobActor.Admin, b.AdminId, null, D(-1));

        // 0104 · Ana · TC04 quoted yesterday, valid until D+14: to pay the deposit live.
        var j104 = await b.RequestAsync(104, clients[Ana], null, null, ("Rua da Rosa, 77", "1200-385"), lisboa, Occupancy.Empty,
            [new(ElementKind.Room, null, "bedroom", RoomSize.M, null, null, null, [true, true, true, false], true, null, "matte", false)], D(-3));
        await b.ConfirmAsync(j104, D(-1));

        // 0105 · Miguel · TC07 deposit paid with MB WAY, waiting for a painter.
        var j105 = await b.RequestAsync(105, clients[Miguel], null, null, ("Rua Garrett, 19", "1200-203"), lisboa, Occupancy.Empty,
            [Room("bathroom", "anti_damp", 2.5m, 2m, height: 2.4m, damage: true)], D(-6));
        var q105 = await b.ConfirmAsync(j105, D(-5));
        await b.PayAsync(j105, clients[Miguel].Id, PaymentType.Deposit, q105.Breakdown.DepositCents, D(-4), PaymentMethod.MbWay);
        j105.TransitionTo(JobStatus.DepositPaid, JobActor.System, null, null, D(-4));

        // 0106 · Lisboa Rent · TC10 in Oeiras scheduled with Rui, starts today: «Empezar trabajo» live.
        var j106 = await b.RequestAsync(106, clients[LisboaRent], LisboaRent.TaxId, LisboaRent.Company, ("Avenida Marginal, 6300", "2780-322"), oeiras,
            Occupancy.Furnished, Flat, D(-10));
        var q106 = await b.ConfirmAsync(j106, D(-9), [new DraftExtraRequest("anti_damp_treatment", null, null, null)]);
        await b.PayAsync(j106, clients[LisboaRent].Id, PaymentType.Deposit, q106.Breakdown.DepositCents, D(-8), PaymentMethod.Card);
        j106.TransitionTo(JobStatus.DepositPaid, JobActor.System, null, null, D(-8));
        await b.ScheduleAsync(j106, rui, q106, today, D(-7));

        // 0107 · Miguel · TC06 in progress with Rui since yesterday, with progress photos.
        var j107 = await b.RequestAsync(107, clients[Miguel], null, null, ("Rua dos Fanqueiros, 38", "1100-231"), lisboa, Occupancy.Empty,
            [Surface("ceiling", "matte", 5m, 4m)], D(-12));
        var q107 = await b.ConfirmAsync(j107, D(-11));
        await b.PayAsync(j107, clients[Miguel].Id, PaymentType.Deposit, q107.Breakdown.DepositCents, D(-10), PaymentMethod.Card);
        j107.TransitionTo(JobStatus.DepositPaid, JobActor.System, null, null, D(-10));
        await b.ScheduleAsync(j107, rui, q107, today.AddDays(-1), D(-9));
        j107.TransitionTo(JobStatus.InProgress, JobActor.Painter, rui.UserId, null, D(-1, 8));
        await b.SaveAsync();
        await b.PhotosAsync(j107, rui.UserId, Pinta.Modules.Media.Contracts.MediaContext.Progress, ["progress-1.png", "progress-2.png"], null, D(-1, 16));

        // 0108 · Ana · TC02 finished by Rui with result photos; the balance is due.
        var j108 = await b.RequestAsync(108, clients[Ana], null, null, ("Rua de Santa Marta, 61", "1150-297"), lisboa, Occupancy.Furnished, [Bedroom], D(-14));
        var q108 = await b.ConfirmAsync(j108, D(-13));
        await b.PayAsync(j108, clients[Ana].Id, PaymentType.Deposit, q108.Breakdown.DepositCents, D(-12), PaymentMethod.Card);
        j108.TransitionTo(JobStatus.DepositPaid, JobActor.System, null, null, D(-12));
        await b.ScheduleAsync(j108, rui, q108, today.AddDays(-2), D(-11));
        j108.TransitionTo(JobStatus.InProgress, JobActor.Painter, rui.UserId, null, D(-2, 8));
        j108.TransitionTo(JobStatus.Finished, JobActor.Painter, rui.UserId, null, D(-1, 17));
        await b.CompleteAsync(j108);
        await b.SaveAsync();
        await b.PhotosAsync(j108, rui.UserId, Pinta.Modules.Media.Contracts.MediaContext.Result, ["result-1.png", "result-2.png", "result-3.png"], null, D(-1, 16));

        // 0094 · Miguel · TC01 not paid in 15 days: expired.
        var j094 = await b.RequestAsync(94, clients[Miguel], null, null, ("Rua da Madalena, 140", "1100-321"), lisboa, Occupancy.Empty, [Bedroom], D(-22));
        await b.ConfirmAsync(j094, D(-20));
        j094.TransitionTo(JobStatus.Expired, JobActor.System, null, null, D(-4, 3));

        // 0095 · Lisboa Rent · TC12 facade over the maximum: rejected.
        var j095 = await b.RequestAsync(95, clients[LisboaRent], LisboaRent.TaxId, LisboaRent.Company, ("Rua Castilho, 39", "1250-068"), lisboa,
            Occupancy.Empty, [Surface("facade", "exterior", 40m, 20m)], D(-8));
        j095.TransitionTo(JobStatus.Rejected, JobActor.Admin, b.AdminId, "Requer um orçamento à medida.", D(-6));
        await b.SaveAsync();
    }

    private static QuoteElementRequest Surface(string surface, string paint, decimal length, decimal width) =>
        new(ElementKind.Surface, null, null, null, length, width, null, null, false, surface, paint, false);

    private static QuoteElementRequest Room(string type, string paint, decimal length, decimal width, decimal? height = null, bool damage = false, bool ceiling = true) =>
        new(ElementKind.Room, null, type, null, length, width, height ?? 2.6m, [true, true, true, true], ceiling, null, paint, damage);

    private static QuoteElementRequest SizedRoom(string type, RoomSize size) =>
        new(ElementKind.Room, null, type, size, null, null, null, [true, true, true, true], true, null, "matte", false);

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
