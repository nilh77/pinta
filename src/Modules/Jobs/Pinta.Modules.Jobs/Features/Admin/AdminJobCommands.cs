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
using Pinta.Modules.Jobs.Resources;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Quoting.Contracts;
using Pinta.Modules.Payments.Contracts;
using Pinta.SharedKernel.Time;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Jobs.Features.Admin;

/// <summary><see cref="Version"/> is the job's version the admin saw; a newer one answers 409 concurrency_conflict.</summary>
public sealed record ConfirmQuoteRequest(string? Note, bool AcknowledgeLowMargin, uint? Version);

public sealed record ReasonRequest(string Reason, uint? Version);

public sealed record SiteVisitRequest(Guid AssigneeId, DateTimeOffset At, long? PriceCents, uint? Version);

public sealed record VersionRequest(uint? Version);

/// <summary><see cref="ScheduledOn"/> is required to move to <c>scheduled</c> (H5: the admin sets the date).</summary>
public sealed record TransitionRequest(JobStatus To, string Reason, uint? Version, DateOnly? ScheduledOn = null);

public sealed record AdminCancelRequest(string Reason, bool FullRefund, uint? Version);

internal sealed class ConfirmQuoteRequestValidator : AbstractValidator<ConfirmQuoteRequest>
{
    public ConfirmQuoteRequestValidator() => RuleFor(r => r.Note).MaximumLength(1000);
}

internal sealed class ReasonRequestValidator : AbstractValidator<ReasonRequest>
{
    public ReasonRequestValidator() => RuleFor(r => r.Reason).NotEmpty().MaximumLength(500);
}

internal sealed class SiteVisitRequestValidator : AbstractValidator<SiteVisitRequest>
{
    public SiteVisitRequestValidator()
    {
        RuleFor(r => r.AssigneeId).NotEmpty();
        RuleFor(r => r.PriceCents).GreaterThanOrEqualTo(0);
    }
}

internal sealed class TransitionRequestValidator : AbstractValidator<TransitionRequest>
{
    public TransitionRequestValidator()
    {
        RuleFor(r => r.To).IsInEnum();
        RuleFor(r => r.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class AdminCancelRequestValidator : AbstractValidator<AdminCancelRequest>
{
    public AdminCancelRequestValidator() => RuleFor(r => r.Reason).NotEmpty().MaximumLength(500);
}

/// <summary>
/// The admin's actions on a job (A2.3–A2.9, A3.3, A3.4). Every status change goes through <see cref="JobStateMachine"/>:
/// anything else answers 409 invalid_transition.
/// </summary>
internal static class AdminJobCommands
{
    public static void Map(RouteGroupBuilder admin)
    {
        var job = admin.MapGroup("/jobs/{id:guid}");
        job.MapPut("/draft-quote", SaveDraftAsync).WithValidation<DraftQuoteRequest>().WithName("SaveDraftQuote");
        job.MapPost("/confirm-quote", ConfirmAsync).WithValidation<ConfirmQuoteRequest>().WithName("ConfirmQuote");
        job.MapPost("/reject", RejectAsync).WithValidation<ReasonRequest>().WithName("RejectJob");
        job.MapPost("/site-visit", SiteVisitAsync).WithValidation<SiteVisitRequest>().WithName("ScheduleSiteVisit");
        job.MapPost("/site-visit/complete", CompleteSiteVisitAsync).WithName("CompleteSiteVisit");
        job.MapPost("/transition", TransitionAsync).WithValidation<TransitionRequest>().WithName("TransitionJob");
        job.MapGet("/cancellation-preview", PreviewAsync).WithName("AdminCancellationPreview");
        job.MapPost("/cancel", CancelAsync).WithValidation<AdminCancelRequest>().WithName("AdminCancelJob");
        job.ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<Results<Ok<AdminQuote>, ProblemHttpResult>> SaveDraftAsync(
        Guid id, DraftQuoteRequest request, HttpContext http, JobsDbContext db, IQuoteVersions quotes, IJobPayments payments,
        IRequestSettings settings, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        // The price is only edited while the job is in review (docs/states.md).
        if (job.Status != JobStatus.InReview)
        {
            return JobProblems.InvalidTransition();
        }

        var credit = await JobRules.SiteVisitCreditCentsAsync(job, payments, settings, cancellationToken);
        var (draft, errors) = await quotes.SaveDraftAsync(
            job.Id, JobRules.DraftOrigin(job), http.User.GetUserId()!.Value, request, credit, cancellationToken);
        return errors is not null ? ApiProblem.Validation(errors.Errors) : TypedResults.Ok(draft!);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ConfirmAsync(
        Guid id, ConfirmQuoteRequest request, HttpContext http, JobsDbContext db, IQuoteVersions quotes, IPriceSnapshotProvider snapshots,
        IUserDirectory users, IEmailQueue emails, WebLinks links, IJobPayments payments, IRequestSettings settings, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var job = await db.Jobs.SingleOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        if (!JobStateMachine.IsAllowed(job.Status, JobStatus.Quoted, JobActor.Admin))
        {
            return JobProblems.InvalidTransition();
        }

        if (request.Version is { } version && version != job.Version)
        {
            return JobProblems.ConcurrencyConflict();
        }

        var adminId = http.User.GetUserId()!.Value;
        var credit = await JobRules.SiteVisitCreditCentsAsync(job, payments, settings, cancellationToken);
        var confirmed = await quotes.ConfirmDraftAsync(
            job.Id, JobRules.DraftOrigin(job), adminId, request.Note, request.AcknowledgeLowMargin, credit, cancellationToken);
        if (confirmed is null)
        {
            var minimum = (await snapshots.GetCurrentAsync(cancellationToken)).Rules.MinMarginPct;
            return ApiProblem.Validation(new Dictionary<string, string[]>
            {
                ["acknowledgeLowMargin"] = [JobsMessages.Get("low_margin", minimum.ToString("0.#", System.Globalization.CultureInfo.CurrentUICulture))],
            });
        }

        var quote = (await quotes.GetAdminAsync(confirmed.Id, cancellationToken))!;
        job.TransitionTo(JobStatus.Quoted, JobActor.Admin, adminId, null, clock.GetUtcNow());
        job.CurrentQuoteVersionId = confirmed.Id;
        job.QuoteValidUntil = confirmed.ValidUntil;
        job.IsPremium = quote.Breakdown.Premium;
        if (!await db.TrySaveAsync(job, request.Version, cancellationToken))
        {
            return JobProblems.ConcurrencyConflict();
        }

        var client = (await users.FindAsync(job.ClientId, cancellationToken))!;
        await emails.EnqueueAsync(new PriceConfirmedEmail(
            client.Email, client.Locale, client.Name, job.Code, confirmed.TotalCents, confirmed.DepositCents, confirmed.ValidUntil, quote.Note,
            links.ClientJob(job.Id, client.Locale)) { JobId = job.Id }, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RejectAsync(
        Guid id, ReasonRequest request, HttpContext http, JobsDbContext db, IUserDirectory users, IEmailQueue emails, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var job = await db.Jobs.SingleOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        var problem = await MoveAsync(db, job, JobStatus.Rejected, http, request.Reason.Trim(), request.Version, clock, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        await SendRejectedAsync(job, request.Reason.Trim(), users, emails, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> SiteVisitAsync(
        Guid id, SiteVisitRequest request, HttpContext http, JobsDbContext db, IUserDirectory users, IRequestSettings settings,
        IEmailQueue emails, TimeProvider clock, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.SingleOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        // In H4 a partner (admin) goes; painters arrive in H6 (docs/plan.md).
        var assignee = (await users.ListAdminsAsync(cancellationToken)).SingleOrDefault(a => a.Id == request.AssigneeId);
        if (assignee is null)
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["assigneeId"] = [JobsMessages.Get("site_visit_assignee")] });
        }

        if (request.At <= clock.GetUtcNow())
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["at"] = [JobsMessages.Get("site_visit_future")] });
        }

        var price = request.PriceCents is { } cents ? Money.FromCents(cents) : (await settings.GetAsync(cancellationToken)).SiteVisitPrice;
        if (!JobStateMachine.IsAllowed(job.Status, JobStatus.SiteVisit, JobActor.Admin))
        {
            return JobProblems.InvalidTransition();
        }

        job.SiteVisitAt = request.At;
        job.SiteVisitAssigneeId = assignee.Id;
        job.SiteVisitPrice = price;
        job.RequiresSiteVisit = true;
        var problem = await MoveAsync(db, job, JobStatus.SiteVisit, http, null, request.Version, clock, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var client = (await users.FindAsync(job.ClientId, cancellationToken))!;
        foreach (var to in new[] { client, assignee })
        {
            await emails.EnqueueAsync(new SiteVisitScheduledEmail(
                to.Email, to.Locale, to.Name, job.Code, request.At, assignee.Name, Money.ToCents(price)) { JobId = job.Id }, cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> CompleteSiteVisitAsync(
        Guid id, VersionRequest? request, HttpContext http, JobsDbContext db, IRequestSettings settings, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var job = await db.Jobs.SingleOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        // Back to review with a new deadline; the next draft has origin "site_visit".
        return await MoveAsync(db, job, JobStatus.InReview, http, null, request?.Version, clock, cancellationToken,
            await JobRules.ReviewDueOnAsync(settings, clock, cancellationToken)) is { } problem
            ? problem
            : TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> TransitionAsync(
        Guid id, TransitionRequest request, HttpContext http, JobsDbContext db, IRequestSettings settings, IUserDirectory users,
        IEmailQueue emails, TimeProvider clock, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.SingleOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        // A confirmed price and a site visit need their own data: they have their own actions.
        if (request.To is JobStatus.Quoted or JobStatus.SiteVisit && JobStateMachine.IsAllowed(job.Status, request.To, JobActor.Admin))
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["to"] = [JobsMessages.Get("transition_use_action")] });
        }

        if (request.To == JobStatus.Scheduled && JobStateMachine.IsAllowed(job.Status, JobStatus.Scheduled, JobActor.Admin))
        {
            if (request.ScheduledOn is not { } scheduledOn || scheduledOn < BusinessDays.Today(clock))
            {
                return ApiProblem.Validation(new Dictionary<string, string[]> { ["scheduledOn"] = [JobsMessages.Get("scheduled_on")] });
            }

            job.ScheduledOn = scheduledOn;
        }

        var reason = request.Reason.Trim();
        var reviewDueOn = request.To == JobStatus.InReview ? await JobRules.ReviewDueOnAsync(settings, clock, cancellationToken) : null;
        var problem = await MoveAsync(db, job, request.To, http, reason, request.Version, clock, cancellationToken, reviewDueOn);
        if (problem is not null)
        {
            return problem;
        }

        if (request.To == JobStatus.Rejected)
        {
            await SendRejectedAsync(job, reason, users, emails, cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<CancellationPreview>, ProblemHttpResult>> PreviewAsync(
        Guid id, JobsDbContext db, IPriceSnapshotProvider snapshots, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id, cancellationToken);
        return job is null
            ? JobProblems.NotFound()
            : TypedResults.Ok(JobRules.Preview(job, (await snapshots.GetCurrentAsync(cancellationToken)).Rules));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> CancelAsync(
        Guid id, AdminCancelRequest request, HttpContext http, JobsDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.SingleOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        // The refund itself (with the full-refund exception) runs with payments in H5.
        return await MoveAsync(db, job, JobStatus.Cancelled, http, request.Reason.Trim(), request.Version, clock, cancellationToken) is { } problem
            ? problem
            : TypedResults.NoContent();
    }

    /// <summary>Applies an admin transition and saves it; the problem to answer when it is not allowed or stale.</summary>
    private static async Task<ProblemHttpResult?> MoveAsync(
        JobsDbContext db, Job job, JobStatus to, HttpContext http, string? reason, uint? version, TimeProvider clock,
        CancellationToken cancellationToken, Func<DateOnly>? reviewDueOn = null)
    {
        if (version is { } expected && expected != job.Version)
        {
            return JobProblems.ConcurrencyConflict();
        }

        try
        {
            job.TransitionTo(to, JobActor.Admin, http.User.GetUserId(), reason, clock.GetUtcNow(), reviewDueOn);
        }
        catch (InvalidTransitionException)
        {
            return JobProblems.InvalidTransition();
        }

        return await db.TrySaveAsync(job, version, cancellationToken) ? null : JobProblems.ConcurrencyConflict();
    }

    private static async Task SendRejectedAsync(Job job, string reason, IUserDirectory users, IEmailQueue emails, CancellationToken cancellationToken)
    {
        var client = (await users.FindAsync(job.ClientId, cancellationToken))!;
        await emails.EnqueueAsync(new RequestRejectedEmail(client.Email, client.Locale, client.Name, job.Code, reason) { JobId = job.Id }, cancellationToken);
    }
}
