using System.Globalization;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Contracts;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Domain;
using Pinta.Modules.Jobs.Features.Shared;
using Pinta.Modules.Media.Contracts;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Painters.Contracts;
using Pinta.Modules.Payments.Contracts;
using Pinta.Modules.Quoting.Contracts;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Jobs.Features.Client;

public sealed record ClientJobListItem(
    Guid Id,
    string Code,
    JobStatus Status,
    string Locality,
    decimal AreaM2,
    long? TotalCents,
    long? RangeMinCents,
    long? RangeMaxCents,
    bool OverMax,
    DateOnly? QuoteValidUntil,
    DateTimeOffset? SiteVisitAt,
    bool NeedsAction,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A step of the client's timeline; the reason only for rejections and cancellations.</summary>
public sealed record ClientTimelineEntry(JobStatus Status, DateTimeOffset At, string? Reason);

public sealed record ClientSiteVisit(DateTimeOffset At, string VisitorName, long PriceCents);

/// <summary>The painter who accepted the job (C5.4): first name only; every Pinta painter is vetted.</summary>
public sealed record ClientPainter(string FirstName);

/// <summary>
/// GET /api/jobs/{id} (screens 8 and 10). Estimate fields (<see cref="AreaM2"/>, range) come from the client's estimate;
/// <see cref="Quote"/> is the confirmed price when there is one; <see cref="Painter"/> once one accepted it and
/// <see cref="ResultPhotos"/> once finished (C5.4, C5.5). Never rates, painter pay or margin.
/// </summary>
public sealed record ClientJobDetail(
    Guid Id,
    string Code,
    JobStatus Status,
    DateTimeOffset CreatedAt,
    decimal AreaM2,
    long? RangeMinCents,
    long? RangeMaxCents,
    bool OverMax,
    string AddressLine1,
    string PostalCode,
    string Locality,
    PreferredDatesDto PreferredDates,
    DateOnly? ReviewDueOn,
    DateOnly? QuoteValidUntil,
    DateOnly? ScheduledOn,
    ClientQuote? Quote,
    ClientSiteVisit? SiteVisit,
    ClientPainter? Painter,
    IReadOnlyList<JobFile> ResultPhotos,
    IReadOnlyList<ClientTimelineEntry> Timeline,
    bool CanCancel,
    bool CanRequote);

public sealed record RejectQuoteRequest(string? Reason);

public sealed record QuestionRequest(string Message);

public sealed record ClientCancelRequest(string? Reason);

internal sealed class RejectQuoteRequestValidator : AbstractValidator<RejectQuoteRequest>
{
    public RejectQuoteRequestValidator() => RuleFor(r => r.Reason).MaximumLength(500);
}

internal sealed class QuestionRequestValidator : AbstractValidator<QuestionRequest>
{
    public QuestionRequestValidator() => RuleFor(r => r.Message).NotEmpty().MaximumLength(2000);
}

internal sealed class ClientCancelRequestValidator : AbstractValidator<ClientCancelRequest>
{
    public ClientCancelRequestValidator() => RuleFor(r => r.Reason).MaximumLength(500);
}

/// <summary>The client's jobs (C5.1, C5.2, C5.6, C5.7). Another client's job is 404, as if it did not exist.</summary>
internal static class ClientJobEndpoints
{
    private static readonly JobStatus[] Finished = [JobStatus.Closed, JobStatus.Cancelled, JobStatus.Rejected];

    public static void Map(RouteGroupBuilder jobs)
    {
        var client = jobs.MapGroup("").RequireAuthorization(policy => policy.RequireRole(RoleNames.Client));
        client.MapGet("/", ListAsync).WithName("ListMyJobs");
        client.MapGet("/{id:guid}", DetailAsync).WithName("GetJob");
        client.MapPost("/{id:guid}/reject-quote", RejectQuoteAsync).WithValidation<RejectQuoteRequest>().WithName("RejectQuote");
        client.MapPost("/{id:guid}/questions", QuestionAsync).WithValidation<QuestionRequest>().RequireRateLimiting(RateLimitPolicies.PublicForm)
            .WithName("AskAboutQuote");
        client.MapGet("/{id:guid}/cancellation-preview", PreviewAsync).WithName("CancellationPreview");
        client.MapPost("/{id:guid}/cancel", CancelAsync).WithValidation<ClientCancelRequest>().WithName("CancelJob");
        client.MapPost("/{id:guid}/requote", RequoteAsync).WithName("RequestNewQuote");
        client.ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }

    /// <param name="filter"><c>active</c> (default: not closed, cancelled or rejected) or <c>history</c>.</param>
    private static async Task<Results<Ok<Paged<ClientJobListItem>>, ProblemHttpResult>> ListAsync(
        HttpContext http, JobsDbContext db, IQuoteVersions quotes, string? filter, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        if (filter is not (null or "active" or "history"))
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["filter"] = ["Use active or history."] });
        }

        var userId = http.User.GetUserId();
        var jobs = db.Jobs.AsNoTracking().Where(j => j.ClientId == userId);
        jobs = filter == "history" ? jobs.Where(j => Finished.Contains(j.Status)) : jobs.Where(j => !Finished.Contains(j.Status));
        var (p, size) = Paged<ClientJobListItem>.Normalize(page, pageSize);
        var total = await jobs.CountAsync(cancellationToken);
        var items = new List<ClientJobListItem>();
        foreach (var job in await jobs.OrderByDescending(j => j.CreatedAt).Skip((p - 1) * size).Take(size).ToListAsync(cancellationToken))
        {
            var quote = await quotes.GetAsync(job.CurrentQuoteVersionId, cancellationToken);
            var priced = job.QuoteValidUntil is not null;
            items.Add(new ClientJobListItem(
                job.Id, job.Code, job.Status, job.Locality, quote?.AreaM2 ?? 0, priced ? quote?.TotalCents : null, quote?.RangeMinCents,
                quote?.RangeMaxCents, quote?.OverMax ?? false, job.QuoteValidUntil, job.SiteVisitAt, job.Status == JobStatus.Quoted,
                job.CreatedAt, job.UpdatedAt));
        }

        return TypedResults.Ok(new Paged<ClientJobListItem>(items, p, size, total));
    }

    private static async Task<Results<Ok<ClientJobDetail>, ProblemHttpResult>> DetailAsync(
        Guid id, HttpContext http, JobsDbContext db, IQuoteVersions quotes, IUserDirectory users, IPainterDirectory painters, IMediaService media,
        CancellationToken cancellationToken)
    {
        var job = await FindAsync(db, http, id, tracked: false, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        var versions = await quotes.ListAsync(job.Id, cancellationToken);
        var estimate = await quotes.GetAsync(versions[0].Id, cancellationToken);
        var confirmed = versions.Any(v => v.Id == job.CurrentQuoteVersionId && v.ConfirmedAt is not null)
            ? await quotes.GetClientAsync(job.CurrentQuoteVersionId, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, cancellationToken)
            : null;
        var visitor = job.SiteVisitAssigneeId is { } assignee ? await users.FindAsync(assignee, cancellationToken) : null;
        var painterId = await db.Assignments.Where(a => a.JobId == job.Id && (a.Status == AssignmentStatus.Accepted || a.Status == AssignmentStatus.Completed))
            .Select(a => (Guid?)a.PainterId).FirstOrDefaultAsync(cancellationToken);
        var painter = painterId is { } pid ? await painters.FindAsync(pid, cancellationToken) : null;
        var resultPhotos = job.Status is JobStatus.Finished or JobStatus.Closed
            ? [.. (await media.ListForJobAsync(job.Id, cancellationToken)).Where(f => f.Context == MediaContext.Result)]
            : Array.Empty<JobFile>();
        return TypedResults.Ok(new ClientJobDetail(
            job.Id,
            job.Code,
            job.Status,
            job.CreatedAt,
            estimate!.AreaM2,
            estimate.RangeMinCents,
            estimate.RangeMaxCents,
            estimate.OverMax,
            job.AddressLine1,
            job.PostalCode,
            job.Locality,
            new PreferredDatesDto(job.PreferredAsap, job.PreferredFrom, job.PreferredTo),
            job.ReviewDueOn,
            job.QuoteValidUntil,
            job.ScheduledOn,
            confirmed,
            job.SiteVisitAt is { } at ? new ClientSiteVisit(at, visitor?.Name ?? "", Money.ToCents(job.SiteVisitPrice ?? 0)) : null,
            painter is null ? null : new ClientPainter(Assignments.AssignmentEndpoints.FirstName(painter.Name)),
            resultPhotos,
            [.. job.History.OrderBy(h => h.ChangedAt).Select(h => new ClientTimelineEntry(
                h.To, h.ChangedAt, h.To is JobStatus.Rejected or JobStatus.Cancelled ? h.Reason : null))],
            JobStateMachine.CancellableFrom.Contains(job.Status),
            job.Status == JobStatus.Expired));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RejectQuoteAsync(
        Guid id, RejectQuoteRequest request, HttpContext http, JobsDbContext db, IUserDirectory users, IEmailQueue emails, WebLinks links,
        JobCancellation cancellation, TimeProvider clock, CancellationToken cancellationToken)
    {
        var job = await FindAsync(db, http, id, tracked: true, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        // Rejecting is cancelling a confirmed price (C5.2); in any other state there is no price to reject.
        if (job.Status != JobStatus.Quoted)
        {
            return JobProblems.InvalidTransition();
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var preview = await cancellation.PreviewAsync(job, false, cancellationToken);
        if (await MoveAsync(db, job, JobStatus.Cancelled, http, reason, clock, cancellationToken) is { } problem)
        {
            return problem;
        }

        await cancellation.CancelledAsync(job, preview, reason, cancellationToken);
        var client = (await users.FindAsync(job.ClientId, cancellationToken))!;
        foreach (var admin in await users.ListAdminsAsync(cancellationToken))
        {
            await emails.EnqueueAsync(new ClientRejectedPriceEmail(
                admin.Email, admin.Locale, job.Code, client.Name, reason, links.AdminJob(job.Id, admin.Locale)) { JobId = job.Id }, cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> QuestionAsync(
        Guid id, QuestionRequest request, HttpContext http, JobsDbContext db, IUserDirectory users, IEmailQueue emails, WebLinks links,
        CancellationToken cancellationToken)
    {
        var job = await FindAsync(db, http, id, tracked: false, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        // "Tengo dudas" is about the confirmed price and does not change the state (C5.2).
        if (job.Status != JobStatus.Quoted)
        {
            return JobProblems.InvalidTransition();
        }

        var client = (await users.FindAsync(job.ClientId, cancellationToken))!;
        foreach (var admin in await users.ListAdminsAsync(cancellationToken))
        {
            await emails.EnqueueAsync(new ClientQuestionEmail(
                admin.Email, admin.Locale, job.Code, client.Name, client.Email, request.Message.Trim(), links.AdminJob(job.Id, admin.Locale))
            { JobId = job.Id }, cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<CancellationPreview>, ProblemHttpResult>> PreviewAsync(
        Guid id, HttpContext http, JobsDbContext db, JobCancellation cancellation, CancellationToken cancellationToken)
    {
        var job = await FindAsync(db, http, id, tracked: false, cancellationToken);
        return job is null
            ? JobProblems.NotFound()
            : TypedResults.Ok(await cancellation.PreviewAsync(job, false, cancellationToken));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> CancelAsync(
        Guid id, ClientCancelRequest request, HttpContext http, JobsDbContext db, JobCancellation cancellation, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var job = await FindAsync(db, http, id, tracked: true, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var preview = await cancellation.PreviewAsync(job, false, cancellationToken);
        if (await MoveAsync(db, job, JobStatus.Cancelled, http, reason, clock, cancellationToken) is { } problem)
        {
            return problem;
        }

        await cancellation.CancelledAsync(job, preview, reason, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RequoteAsync(
        Guid id, HttpContext http, JobsDbContext db, IQuoteVersions quotes, IRequestSettings settings, IJobPayments payments, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var job = await FindAsync(db, http, id, tracked: true, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        var problem = await MoveAsync(db, job, JobStatus.InReview, http, null, clock, cancellationToken,
            await JobRules.ReviewDueOnAsync(settings, clock, cancellationToken));
        if (problem is not null)
        {
            return problem;
        }

        // A new version with today's prices (C5.7); the admin reviews it as a draft.
        await quotes.OpenDraftAsync(
            job.Id, QuoteOrigin.Revision, job.ClientId, await JobRules.SiteVisitCreditCentsAsync(job, payments, settings, cancellationToken),
            cancellationToken);
        return TypedResults.NoContent();
    }

    private static Task<Job?> FindAsync(JobsDbContext db, HttpContext http, Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var userId = http.User.GetUserId();
        var jobs = db.Jobs.Include(j => j.History).Where(j => j.Id == id && j.ClientId == userId);
        return (tracked ? jobs : jobs.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
    }

    private static async Task<ProblemHttpResult?> MoveAsync(
        JobsDbContext db, Job job, JobStatus to, HttpContext http, string? reason, TimeProvider clock, CancellationToken cancellationToken,
        Func<DateOnly>? reviewDueOn = null)
    {
        try
        {
            job.TransitionTo(to, JobActor.Client, http.User.GetUserId(), reason, clock.GetUtcNow(), reviewDueOn);
        }
        catch (InvalidTransitionException)
        {
            return JobProblems.InvalidTransition();
        }

        return await db.TrySaveAsync(job, null, cancellationToken) ? null : JobProblems.ConcurrencyConflict();
    }
}
