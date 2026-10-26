using Pinta.Modules.Finance.Contracts;
using Pinta.Modules.Finance.Data;
using Pinta.Modules.Finance.Domain;
using Pinta.Modules.Identity.Domain;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Domain;
using Pinta.Modules.Media.Contracts;
using Pinta.Modules.Media.Data;
using Pinta.Modules.Media.Domain;
using Pinta.Modules.Media.Storage;
using Pinta.Modules.Painters.Domain;
using Pinta.Modules.Payments.Contracts;
using Pinta.Modules.Payments.Data;
using Pinta.Modules.Payments.Domain;
using Pinta.Modules.Quoting.Contracts;
using Pinta.Modules.Quoting.Data;
using Pinta.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Pinta.Api.Seed;

/// <summary>
/// Builds demo jobs through the real services (quotes from the engine, payments, assignments, payouts and photos), with
/// the dates the demo needs instead of today's.
/// </summary>
internal sealed class DemoJobBuilder(IServiceProvider services, Guid adminId, int year)
{
    private readonly JobsDbContext jobs = services.GetRequiredService<JobsDbContext>();
    private readonly IQuoteVersions quotes = services.GetRequiredService<IQuoteVersions>();
    private readonly QuotingDbContext quoting = services.GetRequiredService<QuotingDbContext>();
    private readonly PaymentsDbContext payments = services.GetRequiredService<PaymentsDbContext>();
    private readonly FinanceDbContext finance = services.GetRequiredService<FinanceDbContext>();
    private readonly MediaDbContext media = services.GetRequiredService<MediaDbContext>();
    private readonly ObjectStorage storage = services.GetRequiredService<ObjectStorage>();
    private readonly Dictionary<Guid, AdminQuote> estimates = [];

    public Guid AdminId => adminId;

    /// <summary>Every payment made, in euros (for the Stripe fees).</summary>
    public List<decimal> PaymentsMade { get; } = [];

    /// <summary>A request sent by the client on <paramref name="submitted"/>, in review until <paramref name="reviewDueOn"/>.</summary>
    public async Task<Job> RequestAsync(
        int number, User client, string? taxId, string? company, (string Line1, string PostalCode) address, (string Locality, string Municipality) place,
        Occupancy occupancy, IReadOnlyList<QuoteElementRequest> elements, DateTimeOffset submitted, DateOnly? reviewDueOn = null, string? description = null)
    {
        var job = new Job
        {
            Code = $"PT-{year}-{number:0000}",
            ClientId = client.Id,
            AddressLine1 = address.Line1,
            PostalCode = address.PostalCode,
            Locality = place.Locality,
            Municipality = place.Municipality,
            PreferredAsap = true,
            TaxId = taxId,
            CompanyName = company,
            Description = description,
            CreatedAt = submitted,
        };
        var (estimate, errors) = await quotes.CreateEstimateAsync(job.Id, client.Id, occupancy, elements);
        if (errors is not null)
        {
            throw new InvalidOperationException($"Demo job {job.Code}: {string.Join("; ", errors.Errors.SelectMany(e => e.Value))}");
        }

        await quoting.Versions.Where(v => v.Id == estimate!.Id).ExecuteUpdateAsync(v => v.SetProperty(x => x.CreatedAt, submitted));
        job.CurrentQuoteVersionId = estimate!.Id;
        job.IsPremium = estimate.Premium;
        var due = reviewDueOn ?? DateOnly.FromDateTime(submitted.UtcDateTime).AddDays(2);
        job.Submit(true, submitted, () => due);
        jobs.Jobs.Add(job);
        estimates[job.Id] = (await quotes.GetAdminAsync(estimate.Id))!;
        return job;
    }

    /// <summary>The admin confirms the price (with catalog extras if any) on <paramref name="confirmed"/>; valid 15 days.</summary>
    public async Task<AdminQuote> ConfirmAsync(Job job, DateTimeOffset confirmed, IReadOnlyList<DraftExtraRequest>? extras = null)
    {
        await jobs.SaveChangesAsync();
        if (extras is { Count: > 0 })
        {
            var estimate = estimates[job.Id];
            var elements = estimate.Elements.Select(e => new QuoteElementRequest(
                e.Kind, e.Name, e.RoomTypeId, e.Size, e.Length, e.Width, e.Height, e.Walls, e.Ceiling, e.SurfaceTypeId, e.PaintId, e.Damage)).ToList();
            await quotes.SaveDraftAsync(job.Id, QuoteOrigin.AdminReview, adminId, new DraftQuoteRequest(estimate.Occupancy, elements, extras, null, null, null));
        }

        var confirmedQuote = (await quotes.ConfirmDraftAsync(job.Id, QuoteOrigin.AdminReview, adminId, null, acknowledgeLowMargin: true))!;
        var validUntil = DateOnly.FromDateTime(confirmed.UtcDateTime).AddDays(15);
        await quoting.Versions.Where(v => v.Id == confirmedQuote.Id).ExecuteUpdateAsync(v => v
            .SetProperty(x => x.CreatedAt, confirmed.AddHours(-1)).SetProperty(x => x.ConfirmedAt, confirmed).SetProperty(x => x.ValidUntil, validUntil));
        job.TransitionTo(JobStatus.Quoted, JobActor.Admin, adminId, null, confirmed);
        job.CurrentQuoteVersionId = confirmedQuote.Id;
        job.QuoteValidUntil = validUntil;
        await jobs.SaveChangesAsync();
        var quote = (await quotes.GetAdminAsync(confirmedQuote.Id))!;
        job.IsPremium = quote.Breakdown.Premium;
        return quote;
    }

    public async Task<Payment> PayAsync(Job job, Guid clientId, PaymentType type, long cents, DateTimeOffset paidAt, PaymentMethod method)
    {
        var payment = new Payment
        {
            JobId = job.Id, ClientId = clientId, Type = type, Amount = Money.FromCents(cents), Status = PaymentStatus.Succeeded, Method = method,
            StripePaymentIntentId = $"pi_demo_{job.Code}_{type}".ToLowerInvariant(), PaidAt = paidAt, CreatedAt = paidAt.AddMinutes(-3), UpdatedAt = paidAt,
            ReceiptNumber = await payments.NextReceiptNumberAsync(year, CancellationToken.None),
        };
        payments.Payments.Add(payment);
        await payments.SaveChangesAsync();
        PaymentsMade.Add(payment.Amount);
        return payment;
    }

    /// <summary>The painter accepted the offer: the job is scheduled for <paramref name="scheduledOn"/>.</summary>
    public Task ScheduleAsync(Job job, Painter painter, AdminQuote quote, DateOnly scheduledOn, DateTimeOffset acceptedAt)
    {
        jobs.Assignments.Add(new Assignment
        {
            JobId = job.Id, PainterId = painter.Id, ScheduledOn = scheduledOn, PainterPay = Money.FromCents(quote.Breakdown.PainterTotalCents),
            Status = AssignmentStatus.Accepted, OfferedAt = acceptedAt.AddHours(-3), RespondedAt = acceptedAt,
        });
        job.ScheduledOn = scheduledOn;
        job.TransitionTo(JobStatus.Scheduled, JobActor.Painter, painter.UserId, null, acceptedAt);
        return Task.CompletedTask;
    }

    public async Task CompleteAsync(Job job) => await SetAssignmentAsync(job, AssignmentStatus.Completed, null);

    public async Task WithdrawAsync(Job job, DateTimeOffset at) => await SetAssignmentAsync(job, AssignmentStatus.Withdrawn, at);

    private async Task SetAssignmentAsync(Job job, AssignmentStatus status, DateTimeOffset? at)
    {
        var assignment = jobs.Assignments.Local.SingleOrDefault(a => a.JobId == job.Id)
            ?? await jobs.Assignments.SingleAsync(a => a.JobId == job.Id);
        assignment.Status = status;
        if (at is not null)
        {
            assignment.RespondedAt = at;
        }
    }

    public Task SaveAsync() => jobs.SaveChangesAsync();

    public async Task RefundHalfAsync(Payment deposit, int number, DateTimeOffset at)
    {
        deposit.Refunds.Add(new Refund
        {
            PaymentId = deposit.Id, Amount = Math.Round(deposit.Amount / 2, 2, MidpointRounding.AwayFromZero), Reason = "Cancelamento 3 dias antes do início",
            Status = RefundStatus.Succeeded, StripeRefundId = $"re_demo_{number:0000}", CreatedAt = at, CompletedAt = at.AddMinutes(2),
        });
        deposit.Status = PaymentStatus.PartiallyRefunded;
        await payments.SaveChangesAsync();
    }

    public async Task PayoutAsync(Job job, Painter painter, AdminQuote quote, DateTimeOffset closed, DateTimeOffset paid, string reference)
    {
        finance.PainterPayouts.Add(new PainterPayout
        {
            PainterId = painter.Id, JobId = job.Id, JobCode = job.Code, Amount = Money.FromCents(quote.Breakdown.PainterTotalCents), Status = PayoutStatus.Paid,
            Reference = reference, PaidAt = paid, CreatedAt = closed,
        });
        await finance.SaveChangesAsync();
    }

    /// <summary>Generated demo photos (src/Pinta.Api/Seed/Images, tools/demo_images.py) stored and attached to the job.</summary>
    public async Task PhotosAsync(Job job, Guid uploadedBy, MediaContext context, IReadOnlyList<string> images, string? elementKey, DateTimeOffset at)
    {
        var estimate = estimates.GetValueOrDefault(job.Id);
        var elementId = elementKey is null || estimate is null ? (Guid?)null : estimate.Elements.FirstOrDefault(e => e.ClientKey == elementKey)?.Id;
        foreach (var image in images)
        {
            await using var resource = typeof(DemoJobBuilder).Assembly.GetManifestResourceStream($"Pinta.Api.Seed.Images.{image}")
                ?? throw new InvalidOperationException($"Missing demo image {image}.");
            using var copy = new MemoryStream();
            await resource.CopyToAsync(copy);
            var content = copy.ToArray();
            var file = new MediaFile
            {
                JobId = job.Id, UploadedBy = uploadedBy, ElementKey = elementKey, QuoteElementId = elementId, Context = context, Kind = MediaKind.Image,
                ContentType = "image/png", SizeBytes = content.Length, Confirmed = true, CreatedAt = at, ConfirmedAt = at,
                StorageKey = $"jobs/{job.Id:N}/demo/{Guid.CreateVersion7():N}.png",
            };
            await storage.PutAsync(file.StorageKey, content, file.ContentType, CancellationToken.None);
            media.Files.Add(file);
        }

        await media.SaveChangesAsync();
    }
}
