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
using Pinta.Modules.Payments.Contracts;
using Pinta.Modules.Payments.Data;
using Pinta.Modules.Payments.Domain;
using Pinta.Modules.Quoting.Contracts;
using Pinta.Modules.Quoting.Data;
using Pinta.Pricing;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Time;

namespace Pinta.Api.Seed;

/// <summary>
/// The finance part of the demo (docs/plan.md, "Datos de demo" › Finanzas): the three clients and last month with
/// PT-…-0091 (TC08) and PT-…-0092 (TC05) closed by Rui, PT-…-0093 (TC03) cancelled 3 days before with half the deposit
/// kept, the operating costs, Rui's payouts paid and the month closed. Amounts come from the pricing engine.
/// </summary>
internal static class FinanceDemoSeeder
{
    private sealed record Client(string Name, string Email, string Phone, string? TaxId, string? Company);

    private static readonly Client Ana = new("Ana Costa", "ana@demo.pinta.pt", "+351912000101", null, null);
    private static readonly Client Miguel = new("Miguel Rocha", "miguel@demo.pinta.pt", "+351912000102", null, null);
    private static readonly Client LisboaRent = new("Lisboa Rent, Lda.", "gestao@demo.pinta.pt", "+351212000103", "509123457", "Lisboa Rent, Lda.");

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
        var rui = await services.GetRequiredService<PaintersDbContext>().Painters.SingleAsync(p => p.Email == "rui@demo.pinta.pt");
        var today = BusinessDays.Today(services.GetRequiredService<TimeProvider>());
        var month = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        DateTimeOffset On(int day, int hour = 10) => new(month.AddDays(day - 1).ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero);

        var seeder = new JobSeeder(services, nathan.Id, rui.Id, rui.UserId, today.Year);
        var facade = new QuoteElementRequest(ElementKind.Surface, null, null, null, 12m, 6m, null, null, false, "facade", "exterior", false);
        var wall = new QuoteElementRequest(ElementKind.Surface, null, null, null, 3m, 2.6m, null, null, false, "interior_wall", "matte", false);
        var bedroom = new QuoteElementRequest(ElementKind.Room, null, "bedroom", null, 4m, 3m, 2.6m, [true, true, true, true], true, null, "matte", false);

        await seeder.ClosedAsync(91, clients[LisboaRent], LisboaRent, ("Avenida da Liberdade, 120", "1250-146"), Occupancy.Empty, facade,
            submitted: On(1), confirmed: On(2), depositPaid: On(3), start: On(8), finished: On(9, 17), closed: On(10), payoutPaid: On(12));
        await seeder.ClosedAsync(92, clients[Miguel], Miguel, ("Rua Augusta, 50", "1100-053"), Occupancy.Empty, wall,
            submitted: On(5), confirmed: On(6), depositPaid: On(6, 18), start: On(14), finished: On(14, 17), closed: On(20), payoutPaid: On(22));
        await seeder.CancelledAsync(93, clients[Ana], Ana, ("Rua de São Bento, 300", "1200-821"), Occupancy.Inhabited, bedroom,
            submitted: On(3), confirmed: On(4), depositPaid: On(5), scheduledOn: month.AddDays(14), cancelled: On(12));

        // New requests are numbered after the demo jobs (H8 seeds up to 0108).
        await services.GetRequiredService<JobsDbContext>().Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO jobs.code_counters (year, last) VALUES ({today.Year}, 120)
            ON CONFLICT (year) DO UPDATE SET last = GREATEST(code_counters.last, 120)
            """);

        // Operating costs: hosting, domain and mail, and Stripe's fees on the five payments (1,5% + 0,25 € each).
        var finance = services.GetRequiredService<FinanceDbContext>();
        var stripeFees = seeder.Payments.Sum(amount => Math.Round(amount * 0.015m, 2, MidpointRounding.AwayFromZero) + 0.25m);
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

    /// <summary>Builds one demo job through its states with the real quote versions, payments and payout.</summary>
    private sealed class JobSeeder(IServiceProvider services, Guid adminId, Guid painterId, Guid painterUserId, int year)
    {
        private readonly JobsDbContext jobs = services.GetRequiredService<JobsDbContext>();
        private readonly IQuoteVersions quotes = services.GetRequiredService<IQuoteVersions>();
        private readonly QuotingDbContext quoting = services.GetRequiredService<QuotingDbContext>();
        private readonly PaymentsDbContext payments = services.GetRequiredService<PaymentsDbContext>();
        private readonly FinanceDbContext finance = services.GetRequiredService<FinanceDbContext>();

        /// <summary>Every payment made, in euros, for the Stripe fees.</summary>
        public List<decimal> Payments { get; } = [];

        public async Task ClosedAsync(
            int number, User client, Client details, (string Line1, string PostalCode) address, Occupancy occupancy, QuoteElementRequest element,
            DateTimeOffset submitted, DateTimeOffset confirmed, DateTimeOffset depositPaid, DateTimeOffset start, DateTimeOffset finished,
            DateTimeOffset closed, DateTimeOffset payoutPaid)
        {
            var (job, quote) = await QuotedAsync(number, client, details, address, occupancy, element, submitted, confirmed);
            await PayAsync(job, client, PaymentType.Deposit, quote.Breakdown.DepositCents, depositPaid);
            job.TransitionTo(JobStatus.DepositPaid, JobActor.System, null, null, depositPaid);
            var scheduledOn = DateOnly.FromDateTime(start.UtcDateTime);
            jobs.Assignments.Add(new Assignment
            {
                JobId = job.Id, PainterId = painterId, ScheduledOn = scheduledOn, PainterPay = Money.FromCents(quote.Breakdown.PainterTotalCents),
                Status = AssignmentStatus.Completed, OfferedAt = depositPaid.AddHours(2), RespondedAt = depositPaid.AddHours(5),
            });
            job.ScheduledOn = scheduledOn;
            job.TransitionTo(JobStatus.Scheduled, JobActor.Painter, painterUserId, null, depositPaid.AddHours(5));
            job.TransitionTo(JobStatus.InProgress, JobActor.Painter, painterUserId, null, start);
            job.TransitionTo(JobStatus.Finished, JobActor.Painter, painterUserId, null, finished);
            await PayAsync(job, client, PaymentType.Balance, quote.Breakdown.BalanceCents, closed);
            job.TransitionTo(JobStatus.Closed, JobActor.System, null, null, closed);
            await jobs.SaveChangesAsync();

            finance.PainterPayouts.Add(new PainterPayout
            {
                PainterId = painterId, JobId = job.Id, JobCode = job.Code, Amount = Money.FromCents(quote.Breakdown.PainterTotalCents),
                Status = PayoutStatus.Paid, Reference = $"TRF-{year}-{number:0000}", PaidAt = payoutPaid, CreatedAt = closed,
            });
            await finance.SaveChangesAsync();
        }

        /// <summary>Cancelled by the client 3 days before the start: half the deposit refunded (C5.6).</summary>
        public async Task CancelledAsync(
            int number, User client, Client details, (string Line1, string PostalCode) address, Occupancy occupancy, QuoteElementRequest element,
            DateTimeOffset submitted, DateTimeOffset confirmed, DateTimeOffset depositPaid, DateOnly scheduledOn, DateTimeOffset cancelled)
        {
            var (job, quote) = await QuotedAsync(number, client, details, address, occupancy, element, submitted, confirmed);
            var deposit = await PayAsync(job, client, PaymentType.Deposit, quote.Breakdown.DepositCents, depositPaid);
            job.TransitionTo(JobStatus.DepositPaid, JobActor.System, null, null, depositPaid);
            jobs.Assignments.Add(new Assignment
            {
                JobId = job.Id, PainterId = painterId, ScheduledOn = scheduledOn, PainterPay = Money.FromCents(quote.Breakdown.PainterTotalCents),
                Status = AssignmentStatus.Withdrawn, OfferedAt = depositPaid.AddHours(2), RespondedAt = cancelled,
            });
            job.ScheduledOn = scheduledOn;
            job.TransitionTo(JobStatus.Scheduled, JobActor.Painter, painterUserId, null, depositPaid.AddHours(5));
            job.TransitionTo(JobStatus.Cancelled, JobActor.Client, client.Id, "Mudança de planos.", cancelled);
            await jobs.SaveChangesAsync();

            var refund = Math.Round(deposit.Amount / 2, 2, MidpointRounding.AwayFromZero);
            deposit.Refunds.Add(new Refund
            {
                PaymentId = deposit.Id, Amount = refund, Reason = "Cancelamento 3 dias antes do início", Status = RefundStatus.Succeeded,
                StripeRefundId = $"re_demo_{number:0000}", CreatedAt = cancelled, CompletedAt = cancelled.AddMinutes(2),
            });
            deposit.Status = PaymentStatus.PartiallyRefunded;
            await payments.SaveChangesAsync();
        }

        private async Task<(Job Job, AdminQuote Quote)> QuotedAsync(
            int number, User client, Client details, (string Line1, string PostalCode) address, Occupancy occupancy, QuoteElementRequest element,
            DateTimeOffset submitted, DateTimeOffset confirmed)
        {
            var job = new Job
            {
                Code = $"PT-{year}-{number:0000}",
                ClientId = client.Id,
                AddressLine1 = address.Line1,
                PostalCode = address.PostalCode,
                Locality = "Lisboa",
                Municipality = "Lisboa",
                PreferredAsap = true,
                TaxId = details.TaxId,
                CompanyName = details.Company,
                CreatedAt = submitted,
            };
            var (estimate, errors) = await quotes.CreateEstimateAsync(job.Id, client.Id, occupancy, [element]);
            if (errors is not null)
            {
                throw new InvalidOperationException($"Demo job {job.Code}: {string.Join("; ", errors.Errors.SelectMany(e => e.Value))}");
            }

            job.CurrentQuoteVersionId = estimate!.Id;
            job.Submit(true, submitted, () => DateOnly.FromDateTime(submitted.UtcDateTime).AddDays(2));
            var confirmedQuote = (await quotes.ConfirmDraftAsync(job.Id, QuoteOrigin.AdminReview, adminId, null, acknowledgeLowMargin: true))!;
            var validUntil = DateOnly.FromDateTime(confirmed.UtcDateTime).AddDays(15);
            // The versions get the demo dates instead of today's.
            await quoting.Versions.Where(v => v.JobId == job.Id).ExecuteUpdateAsync(v => v.SetProperty(x => x.CreatedAt, submitted));
            await quoting.Versions.Where(v => v.Id == confirmedQuote.Id).ExecuteUpdateAsync(v => v
                .SetProperty(x => x.ConfirmedAt, confirmed).SetProperty(x => x.ValidUntil, validUntil));
            job.TransitionTo(JobStatus.Quoted, JobActor.Admin, adminId, null, confirmed);
            job.CurrentQuoteVersionId = confirmedQuote.Id;
            job.QuoteValidUntil = validUntil;
            jobs.Jobs.Add(job);
            await jobs.SaveChangesAsync();
            return (job, (await quotes.GetAdminAsync(confirmedQuote.Id))!);
        }

        private async Task<Payment> PayAsync(Job job, User client, PaymentType type, long cents, DateTimeOffset paidAt)
        {
            var payment = new Payment
            {
                JobId = job.Id, ClientId = client.Id, Type = type, Amount = Money.FromCents(cents), Status = PaymentStatus.Succeeded,
                Method = PaymentMethod.Card, StripePaymentIntentId = $"pi_demo_{job.Code}_{type}".ToLowerInvariant(), PaidAt = paidAt,
                CreatedAt = paidAt.AddMinutes(-3), UpdatedAt = paidAt,
                ReceiptNumber = await payments.NextReceiptNumberAsync(year, CancellationToken.None),
            };
            payments.Payments.Add(payment);
            await payments.SaveChangesAsync();
            Payments.Add(payment.Amount);
            return payment;
        }
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
