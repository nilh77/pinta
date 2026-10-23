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

namespace Pinta.Modules.Jobs.Features.Assignments;

/// <summary>A visit in the painter's list (P1.1): shown in «Próximos» with its own tag.</summary>
public sealed record PainterVisitItem(Guid JobId, string Code, JobStatus JobStatus, string Municipality, string Locality, DateTimeOffset At, decimal AreaM2);

/// <summary>
/// GET /api/painter/visits/{id} (P3.1): where, who and what the client declared, element by element. No amounts at all:
/// neither the visit price nor the job's.
/// </summary>
public sealed record PainterVisitDetail(
    Guid Id,
    string Code,
    JobStatus Status,
    DateTimeOffset At,
    string AddressLine1,
    string PostalCode,
    string Locality,
    string Municipality,
    string? AccessNotes,
    string? Description,
    string ClientName,
    string? ClientPhone,
    Occupancy Occupancy,
    decimal AreaM2,
    IReadOnlyList<PainterElement> Elements,
    IReadOnlyList<JobFile> ClientFiles,
    IReadOnlyList<JobFile> VisitFiles,
    DateTimeOffset? ReportedAt,
    string? Notes);

/// <summary>An extra item the painter found: from the catalog, or free with its description and pay.</summary>
public sealed record VisitExtraRequest(string? CatalogKey, string? Description, long? PainterPayCents);

/// <summary>The visit report (P3.1): the real elements, extra items, photos and a note for the reviewer.</summary>
public sealed record SiteVisitReportRequest(
    IReadOnlyList<QuoteElementRequest> Elements, IReadOnlyList<VisitExtraRequest>? Extras, IReadOnlyList<Guid>? FileIds, string? Notes);

internal sealed class SiteVisitReportRequestValidator : AbstractValidator<SiteVisitReportRequest>
{
    public SiteVisitReportRequestValidator()
    {
        RuleFor(r => r.Elements).NotEmpty();
        RuleFor(r => r.Extras).Must(x => x is null || x.Count <= 20);
        RuleForEach(r => r.Extras).ChildRules(extra =>
        {
            extra.RuleFor(x => x.Description).MaximumLength(200);
            extra.RuleFor(x => x.PainterPayCents).GreaterThanOrEqualTo(0);
            extra.RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.Description) && x.PainterPayCents is not null)
                .When(x => string.IsNullOrWhiteSpace(x.CatalogKey))
                .WithName("description");
        });
        RuleFor(r => r.FileIds).Must(ids => ids is null || ids.Count <= 30);
        RuleFor(r => r.Notes).MaximumLength(2000);
    }
}

/// <summary>The site visit done by a painter (P3.1): see it, upload photos and send the report back to review.</summary>
internal static class PainterVisitEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var visits = app.MapGroup("/api/painter/visits/{id:guid}").WithTags("Painter")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Painter));
        visits.MapGet("", DetailAsync).WithName("GetPainterVisit");
        visits.MapPost("/media/upload-url", UploadAsync).RequireRateLimiting(RateLimitPolicies.Uploads).WithName("CreateVisitUploadUrl")
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge).ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        visits.MapPost("/report", ReportAsync).WithValidation<SiteVisitReportRequest>().WithName("ReportSiteVisit");
        visits.ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }

    /// <summary>The painter's visits still to do, soonest first.</summary>
    public static async Task<IReadOnlyList<PainterVisitItem>> ListAsync(Guid userId, JobsDbContext db, IQuoteVersions quotes, CancellationToken cancellationToken)
    {
        var items = new List<PainterVisitItem>();
        foreach (var job in await db.Jobs.AsNoTracking()
                     .Where(j => j.Status == JobStatus.SiteVisit && j.SiteVisitAssigneeId == userId)
                     .OrderBy(j => j.SiteVisitAt).ToListAsync(cancellationToken))
        {
            var area = (await quotes.GetAsync(job.CurrentQuoteVersionId, cancellationToken))?.AreaM2 ?? 0;
            items.Add(new PainterVisitItem(job.Id, job.Code, job.Status, job.Municipality, job.Locality, job.SiteVisitAt!.Value, area));
        }

        return items;
    }

    private static async Task<Results<Ok<PainterVisitDetail>, ProblemHttpResult>> DetailAsync(
        Guid id, HttpContext http, JobsDbContext db, IQuoteVersions quotes, IUserDirectory users, IRequestSettings settings,
        IMediaService media, CancellationToken cancellationToken)
    {
        var job = await FindOwnAsync(id, http, db, tracked: false, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        // What the client declared: the estimate, or the price the admin had before asking for the visit.
        var quote = (await quotes.GetAdminAsync(job.CurrentQuoteVersionId, cancellationToken))!;
        var litres = quote.Breakdown.Lines.GroupBy(l => l.Element).ToDictionary(g => g.Key, g => g.Sum(l => l.Litres));
        var client = await users.FindAsync(job.ClientId, cancellationToken);
        var showPhone = (await settings.GetAsync(cancellationToken)).ClientPhoneVisibleToPainter;
        var files = await media.ListForJobAsync(job.Id, cancellationToken);
        return TypedResults.Ok(new PainterVisitDetail(
            job.Id,
            job.Code,
            job.Status,
            job.SiteVisitAt!.Value,
            job.AddressLine1,
            job.PostalCode,
            job.Locality,
            job.Municipality,
            job.AccessNotes,
            job.Description,
            client?.Name ?? "",
            showPhone ? client?.Phone : null,
            quote.Occupancy,
            quote.Breakdown.AreaM2,
            [.. quote.Elements.Select((e, i) => new PainterElement(
                e.Id, e.Kind, e.Name, e.RoomTypeId, e.Size, e.Length, e.Width, e.Height, e.Walls, e.Ceiling, e.SurfaceTypeId, e.PaintId, e.Damage,
                e.AreaM2, litres.GetValueOrDefault(i)))],
            [.. files.Where(f => f.Context is MediaContext.Element or MediaContext.JobGeneral)],
            [.. files.Where(f => f.Context == MediaContext.SiteVisit)],
            job.SiteVisitReportedAt,
            job.SiteVisitNotes));
    }

    private static async Task<Results<Ok<SignedUpload>, ProblemHttpResult>> UploadAsync(
        Guid id, ResultUploadRequest request, HttpContext http, JobsDbContext db, IMediaService media, CancellationToken cancellationToken)
    {
        var job = await FindOwnAsync(id, http, db, tracked: false, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        if (job.Status != JobStatus.SiteVisit)
        {
            return JobProblems.InvalidTransition();
        }

        var (upload, refusal) = await media.CreateOwnedUploadAsync(
            new OwnedUploadRequest(MediaContext.SiteVisit, http.User.GetUserId()!.Value, job.Id, null, request.ContentType, request.SizeBytes),
            cancellationToken);
        return refusal switch
        {
            UploadRefusal.UnsupportedType => ApiProblem.Create(StatusCodes.Status415UnsupportedMediaType, ErrorCodes.UnsupportedMediaType),
            UploadRefusal.TooLarge => ApiProblem.Create(StatusCodes.Status413PayloadTooLarge, ErrorCodes.FileTooLarge),
            _ => TypedResults.Ok(upload!),
        };
    }

    /// <summary>
    /// P3.1: the painter's elements and extras become the review draft (origin <c>site_visit</c>; free extras with their client
    /// price to set), the photos are attached and the job goes back to review with a new deadline.
    /// </summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> ReportAsync(
        Guid id, SiteVisitReportRequest request, HttpContext http, JobsDbContext db, IQuoteVersions quotes, IMediaService media,
        IPainterDirectory painters, IUserDirectory users, IJobPayments payments, IRequestSettings settings, IEmailQueue emails, WebLinks links,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var job = await FindOwnAsync(id, http, db, tracked: true, cancellationToken);
        if (job is null)
        {
            return JobProblems.NotFound();
        }

        if (!JobStateMachine.IsAllowed(job.Status, JobStatus.InReview, JobActor.Painter) || job.Status != JobStatus.SiteVisit)
        {
            return JobProblems.InvalidTransition();
        }

        var userId = http.User.GetUserId()!.Value;
        var current = (await quotes.GetAdminAsync(job.CurrentQuoteVersionId, cancellationToken))!;
        var credit = await JobRules.SiteVisitCreditCentsAsync(job, payments, settings, cancellationToken);
        var draft = new DraftQuoteRequest(
            current.Occupancy,
            [.. request.Elements.Select(e => e with { ClientKey = null })],
            [.. (request.Extras ?? []).Select(x => new DraftExtraRequest(
                string.IsNullOrWhiteSpace(x.CatalogKey) ? null : x.CatalogKey.Trim(), x.Description?.Trim(), null, x.PainterPayCents))],
            current.PremiumOverride,
            null,
            null);
        var (_, errors) = await quotes.SaveDraftAsync(job.Id, QuoteOrigin.SiteVisit, userId, draft, credit, cancellationToken);
        if (errors is not null)
        {
            return ApiProblem.Validation(errors.Errors);
        }

        if (request.FileIds is { Count: > 0 } fileIds)
        {
            await media.ConfirmOwnedAsync([.. fileIds.Distinct()], userId, job.Id, null, MediaContext.SiteVisit, cancellationToken);
        }

        var now = clock.GetUtcNow();
        job.SiteVisitNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        job.SiteVisitReportedAt = now;
        job.TransitionTo(JobStatus.InReview, JobActor.Painter, userId, null, now, await JobRules.ReviewDueOnAsync(settings, clock, cancellationToken));
        if (!await db.TrySaveAsync(job, null, cancellationToken))
        {
            return JobProblems.ConcurrencyConflict();
        }

        var painter = await painters.FindByUserAsync(userId, cancellationToken);
        foreach (var admin in await users.ListAdminsAsync(cancellationToken))
        {
            await emails.EnqueueAsync(new SiteVisitReportedEmail(
                admin.Email, admin.Locale, job.Code, painter?.Name ?? "", links.AdminJob(job.Id, admin.Locale)) { JobId = job.Id }, cancellationToken);
        }

        return TypedResults.NoContent();
    }

    /// <summary>A visit assigned to this painter; anyone else's (or a partner's) is 404 (S1).</summary>
    private static async Task<Job?> FindOwnAsync(Guid jobId, HttpContext http, JobsDbContext db, bool tracked, CancellationToken cancellationToken)
    {
        var userId = http.User.GetUserId()!.Value;
        var jobs = tracked ? db.Jobs : db.Jobs.AsNoTracking();
        return await jobs.SingleOrDefaultAsync(j => j.Id == jobId && j.SiteVisitAssigneeId == userId && j.SiteVisitAt != null, cancellationToken);
    }
}
