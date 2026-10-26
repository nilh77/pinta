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
using Pinta.Modules.Media.Contracts;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Painters.Contracts;
using Pinta.Modules.Quoting.Contracts;
using Pinta.Pricing;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;
using Pinta.SharedKernel.Time;

namespace Pinta.Modules.Jobs.Features.Assignments;

/// <summary>An element to paint with its measures and the litres planned for it; names come from <c>GET /api/catalog</c>.</summary>
public sealed record PainterElement(
    Guid Id, ElementKind Kind, string? Name, string? RoomTypeId, RoomSize? Size, decimal? Length, decimal? Width, decimal? Height,
    bool[]? Walls, bool Ceiling, string? SurfaceTypeId, string PaintId, bool Damage, decimal AreaM2, decimal Litres);

/// <summary>Material Pinta delivers: litres per paint.</summary>
public sealed record PaintQuantity(string PaintId, decimal Litres);

/// <summary>An extra item with what the painter is paid for it.</summary>
public sealed record PainterExtra(string Description, long PainterPayCents);

/// <summary>
/// GET /api/painter/jobs/{id} (P1.3). Only the painter's own pay: never the client price, VAT, deposit, balance, rates
/// or margin (CLAUDE.md rule 8). The client's phone only when the settings allow it.
/// </summary>
public sealed record PainterJobDetail(
    Guid Id,
    Guid AssignmentId,
    string Code,
    JobStatus Status,
    DateOnly ScheduledOn,
    string AddressLine1,
    string PostalCode,
    string Locality,
    string Municipality,
    string? AccessNotes,
    string? Description,
    string ClientName,
    string? ClientPhone,
    decimal AreaM2,
    IReadOnlyList<PainterElement> Elements,
    IReadOnlyList<PaintQuantity> Paints,
    IReadOnlyList<PainterExtra> Extras,
    string? ReviewerNote,
    long PainterPayCents,
    IReadOnlyList<JobFile> ClientFiles,
    IReadOnlyList<JobFile> ResultFiles,
    IReadOnlyList<JobFile> ProgressFiles,
    IReadOnlyList<PainterIncident> Incidents,
    bool CanStart,
    int MinResultPhotos);

/// <summary>
/// A photo of the job: <see cref="Context"/> <c>result</c> (the default), <c>progress</c> or <c>incident</c>. The site visit
/// upload takes the same body and ignores it.
/// </summary>
public sealed record ResultUploadRequest(string ContentType, long SizeBytes, MediaContext? Context = null);

public sealed record ProgressRequest(IReadOnlyList<Guid> FileIds);

public sealed record IncidentRequest(string Text, IReadOnlyList<Guid>? FileIds);

/// <summary>An incident as the painter who reported it sees it.</summary>
public sealed record PainterIncident(Guid Id, string Text, DateTimeOffset CreatedAt);

public sealed record FinishJobRequest(IReadOnlyList<Guid> FileIds);

internal sealed class ProgressRequestValidator : AbstractValidator<ProgressRequest>
{
    public ProgressRequestValidator() => RuleFor(r => r.FileIds).NotEmpty().Must(ids => ids.Count <= 20);
}

internal sealed class IncidentRequestValidator : AbstractValidator<IncidentRequest>
{
    public IncidentRequestValidator()
    {
        RuleFor(r => r.Text).NotEmpty().MaximumLength(2000);
        RuleFor(r => r.FileIds).Must(ids => ids is null || ids.Count <= 10);
    }
}

internal sealed class FinishJobRequestValidator : AbstractValidator<FinishJobRequest>
{
    public FinishJobRequestValidator() => RuleFor(r => r.FileIds).NotNull().Must(ids => ids.Count <= 50);
}

/// <summary>The painter's job: detail (P1.3), start on the day (P2.1) and finish with result photos (P2.2).</summary>
internal static class PainterJobEndpoints
{
    public const int MinResultPhotos = 3;

    public static void Map(IEndpointRouteBuilder app)
    {
        var jobs = app.MapGroup("/api/painter/jobs/{id:guid}").WithTags("Painter")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Painter));
        jobs.MapGet("", DetailAsync).WithName("GetPainterJob");
        jobs.MapPost("/media/upload-url", UploadAsync).RequireRateLimiting(RateLimitPolicies.Uploads).WithName("CreateResultUploadUrl")
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge).ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        jobs.MapPost("/start", StartAsync).WithName("StartJob");
        jobs.MapPost("/finish", FinishAsync).WithValidation<FinishJobRequest>().WithName("FinishJob");
        jobs.MapPost("/progress", ProgressAsync).WithValidation<ProgressRequest>().WithName("AddProgressPhotos");
        jobs.MapPost("/incidents", IncidentAsync).WithValidation<IncidentRequest>().WithName("ReportIncident");
        jobs.ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<Results<Ok<PainterJobDetail>, ProblemHttpResult>> DetailAsync(
        Guid id, HttpContext http, JobsDbContext db, IPainterDirectory painters, IQuoteVersions quotes, IUserDirectory users,
        IRequestSettings settings, IMediaService media, TimeProvider clock, CancellationToken cancellationToken)
    {
        var (job, assignment) = await FindOwnAsync(id, http, db, painters, tracked: false, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        var quote = (await quotes.GetAdminAsync(job.CurrentQuoteVersionId, cancellationToken))!;
        var litres = quote.Breakdown.Lines.GroupBy(l => l.Element).ToDictionary(g => g.Key, g => g.Sum(l => l.Litres));
        var elements = quote.Elements.Select((e, i) => new PainterElement(
            e.Id, e.Kind, e.Name, e.RoomTypeId, e.Size, e.Length, e.Width, e.Height, e.Walls, e.Ceiling, e.SurfaceTypeId, e.PaintId, e.Damage,
            e.AreaM2, litres.GetValueOrDefault(i))).ToList();
        var client = await users.FindAsync(job.ClientId, cancellationToken);
        var showPhone = (await settings.GetAsync(cancellationToken)).ClientPhoneVisibleToPainter;
        var files = await media.ListForJobAsync(job.Id, cancellationToken);
        return TypedResults.Ok(new PainterJobDetail(
            job.Id,
            assignment!.Id,
            job.Code,
            job.Status,
            assignment.ScheduledOn,
            job.AddressLine1,
            job.PostalCode,
            job.Locality,
            job.Municipality,
            job.AccessNotes,
            job.Description,
            client?.Name ?? "",
            showPhone ? client?.Phone : null,
            quote.Breakdown.AreaM2,
            elements,
            [.. elements.GroupBy(e => e.PaintId).Select(g => new PaintQuantity(g.Key, g.Sum(e => e.Litres)))],
            [.. quote.Extras.Select(x => new PainterExtra(x.Description, x.PainterPayCents))],
            quote.Note,
            Money.ToCents(assignment.PainterPay),
            [.. files.Where(f => f.Context is MediaContext.Element or MediaContext.JobGeneral)],
            [.. files.Where(f => f.Context == MediaContext.Result)],
            [.. files.Where(f => f.Context == MediaContext.Progress)],
            [.. (await db.Incidents.AsNoTracking().Where(i => i.JobId == job.Id).OrderBy(i => i.CreatedAt).ToListAsync(cancellationToken))
                .Select(i => new PainterIncident(i.Id, i.Text, i.CreatedAt))],
            CanStart(job, clock),
            MinResultPhotos));
    }

    private static async Task<Results<Ok<SignedUpload>, ProblemHttpResult>> UploadAsync(
        Guid id, ResultUploadRequest request, HttpContext http, JobsDbContext db, IPainterDirectory painters, IMediaService media,
        CancellationToken cancellationToken)
    {
        var (job, _) = await FindOwnAsync(id, http, db, painters, tracked: false, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        // Result and progress photos while the work runs; incident photos also before it starts.
        var context = request.Context ?? MediaContext.Result;
        var allowed = context switch
        {
            MediaContext.Result or MediaContext.Progress => job.Status == JobStatus.InProgress,
            MediaContext.Incident => job.Status is JobStatus.Scheduled or JobStatus.InProgress,
            _ => false,
        };
        if (!allowed)
        {
            return JobProblems.InvalidTransition();
        }

        var (upload, refusal) = await media.CreateOwnedUploadAsync(
            new OwnedUploadRequest(context, http.User.GetUserId()!.Value, job.Id, null, request.ContentType, request.SizeBytes), cancellationToken);
        return refusal switch
        {
            UploadRefusal.UnsupportedType => ApiProblem.Create(StatusCodes.Status415UnsupportedMediaType, ErrorCodes.UnsupportedMediaType),
            UploadRefusal.TooLarge => ApiProblem.Create(StatusCodes.Status413PayloadTooLarge, ErrorCodes.FileTooLarge),
            _ => TypedResults.Ok(upload!),
        };
    }

    /// <summary>P2.1: only from the scheduled day (Lisbon time).</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> StartAsync(
        Guid id, HttpContext http, JobsDbContext db, IPainterDirectory painters, TimeProvider clock, CancellationToken cancellationToken)
    {
        var (job, _) = await FindOwnAsync(id, http, db, painters, tracked: true, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        if (!CanStart(job, clock))
        {
            return JobProblems.InvalidTransition();
        }

        job.TransitionTo(JobStatus.InProgress, JobActor.Painter, http.User.GetUserId(), null, clock.GetUtcNow());
        return await db.TrySaveAsync(job, null, cancellationToken) ? TypedResults.NoContent() : JobProblems.ConcurrencyConflict();
    }

    /// <summary>P2.2: at least <see cref="MinResultPhotos"/> uploaded result photos; the client is asked for the balance.</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> FinishAsync(
        Guid id, FinishJobRequest request, HttpContext http, JobsDbContext db, IPainterDirectory painters, IMediaService media,
        IQuoteVersions quotes, IUserDirectory users, IEmailQueue emails, WebLinks links, TimeProvider clock, CancellationToken cancellationToken)
    {
        var (job, assignment) = await FindOwnAsync(id, http, db, painters, tracked: true, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        if (!JobStateMachine.IsAllowed(job.Status, JobStatus.Finished, JobActor.Painter))
        {
            return JobProblems.InvalidTransition();
        }

        var userId = http.User.GetUserId()!.Value;
        var photos = await media.ConfirmOwnedAsync([.. request.FileIds.Distinct()], userId, job.Id, null, MediaContext.Result, cancellationToken);
        if (photos.Count(p => p.Kind == MediaKind.Image) < MinResultPhotos)
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["fileIds"] = [JobsMessages.Get("result_photos")] });
        }

        job.TransitionTo(JobStatus.Finished, JobActor.Painter, userId, null, clock.GetUtcNow());
        assignment!.Status = AssignmentStatus.Completed;
        if (!await db.TrySaveAsync(job, null, cancellationToken))
        {
            return JobProblems.ConcurrencyConflict();
        }

        var client = await users.FindAsync(job.ClientId, cancellationToken);
        if (client is not null)
        {
            var language = client.Locale.ToString().ToLowerInvariant();
            var balance = (await quotes.GetClientAsync(job.CurrentQuoteVersionId, language, cancellationToken))?.BalanceCents ?? 0;
            await emails.EnqueueAsync(new JobFinishedEmail(
                client.Email, client.Locale, client.Name, job.Code, balance, links.ClientJob(job.Id, client.Locale)) { JobId = job.Id }, cancellationToken);
        }

        return TypedResults.NoContent();
    }

    /// <summary>P2: progress photos while the job runs; the client sees them on screen 10.</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> ProgressAsync(
        Guid id, ProgressRequest request, HttpContext http, JobsDbContext db, IPainterDirectory painters, IMediaService media,
        CancellationToken cancellationToken)
    {
        var (job, _) = await FindOwnAsync(id, http, db, painters, tracked: false, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        if (job.Status != JobStatus.InProgress)
        {
            return JobProblems.InvalidTransition();
        }

        var photos = await media.ConfirmOwnedAsync([.. request.FileIds.Distinct()], http.User.GetUserId()!.Value, job.Id, null, MediaContext.Progress,
            cancellationToken);
        return photos.Count == 0
            ? ApiProblem.Validation(new Dictionary<string, string[]> { ["fileIds"] = [JobsMessages.Get("progress_photos")] })
            : TypedResults.NoContent();
    }

    /// <summary>P2: an incident with text and photos, scheduled or in progress; the admins get an email.</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> IncidentAsync(
        Guid id, IncidentRequest request, HttpContext http, JobsDbContext db, IPainterDirectory painters, IMediaService media,
        IUserDirectory users, IEmailQueue emails, WebLinks links, TimeProvider clock, CancellationToken cancellationToken)
    {
        var (job, assignment) = await FindOwnAsync(id, http, db, painters, tracked: false, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        if (job.Status is not (JobStatus.Scheduled or JobStatus.InProgress))
        {
            return JobProblems.InvalidTransition();
        }

        var userId = http.User.GetUserId()!.Value;
        var photos = request.FileIds is { Count: > 0 } ids
            ? await media.ConfirmOwnedAsync([.. ids.Distinct()], userId, job.Id, null, MediaContext.Incident, cancellationToken)
            : [];
        db.Incidents.Add(new JobIncident
        {
            JobId = job.Id, PainterId = assignment!.PainterId, Text = request.Text.Trim(), FileIds = [.. photos.Select(p => p.Id)], CreatedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(cancellationToken);

        var painter = await painters.FindAsync(assignment.PainterId, cancellationToken);
        foreach (var admin in await users.ListAdminsAsync(cancellationToken))
        {
            await emails.EnqueueAsync(new IncidentReportedEmail(
                admin.Email, admin.Locale, job.Code, painter?.Name ?? "", request.Text.Trim(), links.AdminJob(job.Id, admin.Locale)) { JobId = job.Id }, cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static bool CanStart(Job job, TimeProvider clock) =>
        JobStateMachine.IsAllowed(job.Status, JobStatus.InProgress, JobActor.Painter) && job.ScheduledOn is { } day && day <= BusinessDays.Today(clock);

    /// <summary>A job whose offer the painter accepted (P1.3); offers, other painters' jobs and unknown ids are 404 (S1).</summary>
    private static async Task<(Job? Job, Assignment? Assignment)> FindOwnAsync(
        Guid jobId, HttpContext http, JobsDbContext db, IPainterDirectory painters, bool tracked, CancellationToken cancellationToken)
    {
        var painter = await painters.FindByUserAsync(http.User.GetUserId()!.Value, cancellationToken);
        if (painter is null)
        {
            return (null, null);
        }

        var assignments = tracked ? db.Assignments : db.Assignments.AsNoTracking();
        var assignment = await assignments.SingleOrDefaultAsync(
            a => a.JobId == jobId && a.PainterId == painter.Id && (a.Status == AssignmentStatus.Accepted || a.Status == AssignmentStatus.Completed),
            cancellationToken);
        if (assignment is null)
        {
            return (null, null);
        }

        var jobs = tracked ? db.Jobs : db.Jobs.AsNoTracking();
        return (await jobs.SingleAsync(j => j.Id == jobId, cancellationToken), assignment);
    }
}
