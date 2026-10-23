using FluentValidation;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Domain;
using Pinta.Modules.Jobs.Features.Shared;
using Pinta.Modules.Jobs.Resources;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Painters.Contracts;
using Pinta.Modules.Quoting.Contracts;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;
using Pinta.SharedKernel.Time;

namespace Pinta.Modules.Jobs.Features.Assignments;

public sealed record OfferJobRequest(Guid PainterId, DateOnly ScheduledOn, uint? Version);

public sealed record DeclineRequest(string Reason);

public sealed record AssignablePainter(Guid Id, string Name, string? Phone, decimal? Rating);

public sealed record AssignmentDto(
    Guid Id, Guid PainterId, string PainterName, DateOnly ScheduledOn, AssignmentStatus Status, long PainterPayCents, DateTimeOffset OfferedAt,
    DateTimeOffset? RespondedAt, string? DeclineReason);

/// <summary>A job as the painter sees it in lists (P1.1): never the client price, VAT, deposit or margin.</summary>
public sealed record PainterJobItem(
    Guid AssignmentId, Guid JobId, string Code, JobStatus JobStatus, AssignmentStatus Status, string Municipality, string Locality,
    DateOnly ScheduledOn, decimal AreaM2, long PainterPayCents, DateTimeOffset OfferedAt);

public sealed record PainterJobs(
    IReadOnlyList<PainterJobItem> Offers, IReadOnlyList<PainterJobItem> Upcoming, IReadOnlyList<PainterJobItem> InProgress, IReadOnlyList<PainterJobItem> Done);

internal sealed class OfferJobRequestValidator : AbstractValidator<OfferJobRequest>
{
    public OfferJobRequestValidator() => RuleFor(r => r.PainterId).NotEmpty();
}

internal sealed class DeclineRequestValidator : AbstractValidator<DeclineRequest>
{
    public DeclineRequestValidator() => RuleFor(r => r.Reason).NotEmpty().MaximumLength(500);
}

/// <summary>Offers to painters (A3.2) and their answer (P1.1, P1.2).</summary>
internal static class AssignmentEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/jobs/{id:guid}").WithTags("Admin jobs")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
        admin.MapGet("/assignable-painters", AssignableAsync).WithName("ListAssignablePainters");
        admin.MapPost("/assignments", OfferAsync).WithValidation<OfferJobRequest>().WithName("OfferJob")
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        var painter = app.MapGroup("/api/painter").WithTags("Painter")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Painter));
        painter.MapGet("/jobs", ListAsync).WithName("ListPainterJobs");
        painter.MapPost("/assignments/{id:guid}/accept", AcceptAsync).WithName("AcceptAssignment")
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        painter.MapPost("/assignments/{id:guid}/decline", DeclineAsync).WithValidation<DeclineRequest>().WithName("DeclineAssignment")
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<Results<Ok<IReadOnlyList<AssignablePainter>>, ProblemHttpResult>> AssignableAsync(
        Guid id, JobsDbContext db, IPainterDirectory painters, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id, cancellationToken);
        return job is null
            ? JobProblems.NotFound()
            : TypedResults.Ok<IReadOnlyList<AssignablePainter>>([.. (await painters.CoveringAsync(job.Municipality, cancellationToken))
                .Select(p => new AssignablePainter(p.Id, p.Name, p.Phone, p.Rating))]);
    }

    /// <summary>
    /// A3.2: the job must have its deposit paid; the painter must be active and cover the concelho; a previous open
    /// offer is withdrawn. The date and the painter's pay are frozen in the offer.
    /// </summary>
    private static async Task<Results<Ok<AssignmentDto>, ProblemHttpResult>> OfferAsync(
        Guid id, OfferJobRequest request, JobsDbContext db, IPainterDirectory painters, IQuoteVersions quotes, IUserDirectory users,
        IEmailQueue emails, WebLinks links, TimeProvider clock, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.SingleOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        if (job.Status != JobStatus.DepositPaid)
        {
            return JobProblems.InvalidTransition();
        }

        if (request.Version is { } version && version != job.Version)
        {
            return JobProblems.ConcurrencyConflict();
        }

        var painter = await painters.FindAsync(request.PainterId, cancellationToken);
        if (painter is null || painter.Status != PainterStatus.Active || !painter.Municipalities.Contains(job.Municipality))
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["painterId"] = [JobsMessages.Get("painter_not_available")] });
        }

        if (request.ScheduledOn < BusinessDays.Today(clock))
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["scheduledOn"] = [JobsMessages.Get("scheduled_on")] });
        }

        var now = clock.GetUtcNow();
        foreach (var open in await db.Assignments.Where(a => a.JobId == job.Id && a.Status == AssignmentStatus.Offered).ToListAsync(cancellationToken))
        {
            open.Status = AssignmentStatus.Withdrawn;
            open.RespondedAt = now;
        }

        var quote = (await quotes.GetAdminAsync(job.CurrentQuoteVersionId, cancellationToken))!;
        var assignment = new Assignment
        {
            JobId = job.Id,
            PainterId = painter.Id,
            ScheduledOn = request.ScheduledOn,
            PainterPay = Money.FromCents(quote.Breakdown.PainterTotalCents),
            Status = AssignmentStatus.Offered,
            OfferedAt = now,
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);

        var locale = (await users.FindAsync(painter.UserId, cancellationToken))?.Locale ?? Locale.Pt;
        await emails.EnqueueAsync(new AssignmentOfferedEmail(
            painter.Email, locale, painter.Name, job.Code, job.Municipality, assignment.ScheduledOn, quote.Breakdown.PainterTotalCents,
            links.PainterHome(locale)) { JobId = job.Id }, cancellationToken);
        return TypedResults.Ok(ToDto(assignment, painter.Name));
    }

    private static async Task<Results<Ok<PainterJobs>, ProblemHttpResult>> ListAsync(
        HttpContext http, JobsDbContext db, IPainterDirectory painters, IQuoteVersions quotes, CancellationToken cancellationToken)
    {
        var painter = await painters.FindByUserAsync(http.User.GetUserId()!.Value, cancellationToken);
        if (painter is null)
        {
            return JobProblems.NotFound();
        }

        var rows = await (from a in db.Assignments.AsNoTracking()
                          join j in db.Jobs.AsNoTracking() on a.JobId equals j.Id
                          where a.PainterId == painter.Id && (a.Status == AssignmentStatus.Offered || a.Status == AssignmentStatus.Accepted || a.Status == AssignmentStatus.Completed)
                          orderby a.ScheduledOn
                          select new { Assignment = a, Job = j }).ToListAsync(cancellationToken);
        var items = new List<PainterJobItem>();
        foreach (var row in rows)
        {
            var area = (await quotes.GetAsync(row.Job.CurrentQuoteVersionId, cancellationToken))?.AreaM2 ?? 0;
            items.Add(new PainterJobItem(
                row.Assignment.Id, row.Job.Id, row.Job.Code, row.Job.Status, row.Assignment.Status, row.Job.Municipality, row.Job.Locality,
                row.Assignment.ScheduledOn, area, Money.ToCents(row.Assignment.PainterPay), row.Assignment.OfferedAt));
        }

        return TypedResults.Ok(new PainterJobs(
            [.. items.Where(i => i.Status == AssignmentStatus.Offered)],
            [.. items.Where(i => i.Status == AssignmentStatus.Accepted && i.JobStatus == JobStatus.Scheduled)],
            [.. items.Where(i => i.Status == AssignmentStatus.Accepted && i.JobStatus == JobStatus.InProgress)],
            [.. items.Where(i => i.Status == AssignmentStatus.Completed || i.JobStatus is JobStatus.Finished or JobStatus.Closed).OrderByDescending(i => i.ScheduledOn)]));
    }

    /// <summary>P1.2: accepting schedules the job with the offer's date; an offer no longer open is <c>assignment_taken</c>.</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> AcceptAsync(
        Guid id, HttpContext http, JobsDbContext db, IPainterDirectory painters, IUserDirectory users, IEmailQueue emails, WebLinks links,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var (assignment, painter, problem) = await FindOwnAsync(id, http, db, painters, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var job = await db.Jobs.SingleAsync(j => j.Id == assignment!.JobId, cancellationToken);
        if (assignment!.Status != AssignmentStatus.Offered || !JobStateMachine.IsAllowed(job.Status, JobStatus.Scheduled, JobActor.Painter))
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.AssignmentTaken);
        }

        var now = clock.GetUtcNow();
        assignment.Status = AssignmentStatus.Accepted;
        assignment.RespondedAt = now;
        job.ScheduledOn = assignment.ScheduledOn;
        job.TransitionTo(JobStatus.Scheduled, JobActor.Painter, http.User.GetUserId(), null, now);
        if (!await db.TrySaveAsync(job, null, cancellationToken))
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.AssignmentTaken);
        }

        var client = await users.FindAsync(job.ClientId, cancellationToken);
        if (client is not null)
        {
            await emails.EnqueueAsync(new JobScheduledEmail(
                client.Email, client.Locale, client.Name, job.Code, assignment.ScheduledOn, FirstName(painter!.Name), links.ClientJob(job.Id, client.Locale))
            { JobId = job.Id }, cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeclineAsync(
        Guid id, DeclineRequest request, HttpContext http, JobsDbContext db, IPainterDirectory painters, IUserDirectory users, IEmailQueue emails,
        WebLinks links, TimeProvider clock, CancellationToken cancellationToken)
    {
        var (assignment, painter, problem) = await FindOwnAsync(id, http, db, painters, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        if (assignment!.Status != AssignmentStatus.Offered)
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.AssignmentTaken);
        }

        assignment.Status = AssignmentStatus.Declined;
        assignment.DeclineReason = request.Reason.Trim();
        assignment.RespondedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        var code = await db.Jobs.Where(j => j.Id == assignment.JobId).Select(j => j.Code).SingleAsync(cancellationToken);
        foreach (var admin in await users.ListAdminsAsync(cancellationToken))
        {
            await emails.EnqueueAsync(new AssignmentDeclinedEmail(
                admin.Email, admin.Locale, code, painter!.Name, assignment.DeclineReason, links.AdminJob(assignment.JobId, admin.Locale))
            { JobId = assignment.JobId }, cancellationToken);
        }

        return TypedResults.NoContent();
    }

    /// <summary>The painter's own assignment; anyone else's is 404 (S1).</summary>
    private static async Task<(Assignment? Assignment, PainterSummary? Painter, ProblemHttpResult? Problem)> FindOwnAsync(
        Guid assignmentId, HttpContext http, JobsDbContext db, IPainterDirectory painters, CancellationToken cancellationToken)
    {
        var painter = await painters.FindByUserAsync(http.User.GetUserId()!.Value, cancellationToken);
        var assignment = painter is null
            ? null
            : await db.Assignments.SingleOrDefaultAsync(a => a.Id == assignmentId && a.PainterId == painter.Id, cancellationToken);
        return assignment is null ? (null, null, JobProblems.NotFound()) : (assignment, painter, null);
    }

    internal static AssignmentDto ToDto(Assignment a, string painterName) => new(
        a.Id, a.PainterId, painterName, a.ScheduledOn, a.Status, Money.ToCents(a.PainterPay), a.OfferedAt, a.RespondedAt, a.DeclineReason);

    internal static string FirstName(string name) => name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? name;
}

/// <summary>Hourly (P1.2): an offer unanswered for 48 h is reported once to the admins.</summary>
public sealed class OfferReminders(JobsDbContext db, IPainterDirectory painters, IUserDirectory users, IEmailQueue emails, WebLinks links, TimeProvider clock)
{
    public const string JobId = "offer-reminders";

    [AutomaticRetry(Attempts = 3)]
    public async Task<int> RunAsync()
    {
        var now = clock.GetUtcNow();
        var cutoff = now.AddHours(-48);
        var stale = await db.Assignments.Where(a => a.Status == AssignmentStatus.Offered && a.ReminderSentAt == null && a.OfferedAt < cutoff).ToListAsync();
        var admins = stale.Count == 0 ? [] : await users.ListAdminsAsync();
        foreach (var assignment in stale)
        {
            var code = await db.Jobs.Where(j => j.Id == assignment.JobId).Select(j => j.Code).SingleAsync();
            var painter = await painters.FindAsync(assignment.PainterId);
            foreach (var admin in admins)
            {
                await emails.EnqueueAsync(new AssignmentUnansweredEmail(
                    admin.Email, admin.Locale, code, painter?.Name ?? "", links.AdminJob(assignment.JobId, admin.Locale)) { JobId = assignment.JobId });
            }

            assignment.ReminderSentAt = now;
        }

        await db.SaveChangesAsync();
        return stale.Count;
    }
}

internal sealed class PainterWork(JobsDbContext db) : IPainterWork
{
    public async Task<IReadOnlyList<PainterJob>> ForPainterAsync(Guid painterId, CancellationToken cancellationToken = default) =>
        [.. (await (from a in db.Assignments.AsNoTracking()
                    join j in db.Jobs.AsNoTracking() on a.JobId equals j.Id
                    where a.PainterId == painterId && (a.Status == AssignmentStatus.Accepted || a.Status == AssignmentStatus.Completed)
                    orderby a.ScheduledOn descending
                    select new { a, j }).ToListAsync(cancellationToken))
            .Select(r => new PainterJob(r.j.Id, r.j.Code, r.j.Status, r.a.Status, r.a.ScheduledOn, Money.ToCents(r.a.PainterPay)))];
}
