using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Domain;
using Pinta.Modules.Jobs.Features.Shared;
using Pinta.Modules.Media.Contracts;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Quoting.Contracts;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;
using Pinta.SharedKernel.Time;

namespace Pinta.Modules.Jobs.Features.Admin;

/// <summary>Serialized as <c>green</c>, <c>amber</c>, <c>red</c> (A2.1).</summary>
public enum ReviewLight
{
    Green,
    Amber,
    Red,
}

public sealed record ReviewItem(
    Guid Id,
    string Code,
    JobStatus Status,
    string ClientName,
    string Municipality,
    decimal AreaM2,
    long? RangeMinCents,
    long? RangeMaxCents,
    bool OverMax,
    bool Premium,
    DateTimeOffset CreatedAt,
    DateOnly? ReviewDueOn,
    ReviewLight? Light);

public sealed record ReviewQueue(IReadOnlyList<ReviewItem> InReview, IReadOnlyList<ReviewItem> PendingEmail);

public sealed record AdminJobListItem(
    Guid Id,
    string Code,
    JobStatus Status,
    string ClientName,
    string ClientEmail,
    string Locality,
    string Municipality,
    long TotalCents,
    long MarginCents,
    bool Premium,
    string? PainterName,
    DateOnly? ScheduledOn,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Filters of A3.1. <see cref="Status"/> takes the snake_case values and may repeat.</summary>
public sealed record AdminJobsQuery(
    [property: FromQuery(Name = "status")] string[]? Status,
    DateOnly? From,
    DateOnly? To,
    string? Municipality,
    bool? Premium,
    string? Q,
    int? Page,
    int? PageSize);

public sealed record ClientInfo(Guid Id, string Name, string Email, string? Phone, Locale Locale, bool EmailConfirmed);

public sealed record AddressDto(string Line1, string? Floor, string PostalCode, string Locality, string Municipality, string? AccessNotes);

public sealed record SiteVisitDto(DateTimeOffset At, Guid AssigneeId, string AssigneeName, long PriceCents);

public sealed record JobHistoryEntry(JobStatus? From, JobStatus To, JobActor Actor, string? UserName, string? Reason, DateTimeOffset ChangedAt);

public sealed record AdminJobDetail(
    Guid Id,
    string Code,
    JobStatus Status,
    uint Version,
    ClientInfo Client,
    AddressDto Address,
    PreferredDatesDto PreferredDates,
    string? Description,
    string? TaxId,
    string? CompanyName,
    bool Premium,
    DateOnly? ReviewDueOn,
    ReviewLight? Light,
    DateOnly? QuoteValidUntil,
    SiteVisitDto? SiteVisit,
    AdminQuote CurrentQuote,
    AdminQuote? Draft,
    IReadOnlyList<QuoteVersionListItem> Versions,
    IReadOnlyList<JobFile> Files,
    IReadOnlyList<JobHistoryEntry> History,
    IReadOnlyList<SentEmail> Emails,
    IReadOnlyList<JobStatus> AllowedTransitions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PartnerDto(Guid Id, string Name, string Email);

/// <summary>GET /api/admin/reviews, /api/admin/jobs (+ export), /api/admin/jobs/{id} and /api/admin/partners (A2.1, A2.2, A3.1, A3.5).</summary>
internal static class AdminJobQueries
{
    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapGet("/reviews", ReviewsAsync).WithName("GetReviewQueue");
        admin.MapGet("/jobs", ListAsync).WithName("ListAdminJobs");
        admin.MapGet("/jobs/export", ExportAsync).WithName("ExportAdminJobs").Produces<string>(StatusCodes.Status200OK, "text/csv");
        admin.MapGet("/jobs/{id:guid}", DetailAsync).WithName("GetAdminJob").ProducesProblem(StatusCodes.Status404NotFound);
        admin.MapGet("/partners", PartnersAsync).WithName("ListPartners");
    }

    public static ReviewLight? Light(Job job, DateTimeOffset now)
    {
        if (job.Status != JobStatus.InReview || job.ReviewDueOn is not { } dueOn)
        {
            return null;
        }

        var left = BusinessDays.WorkingHoursLeft(now, dueOn);
        return left <= 0 ? ReviewLight.Red : left <= 24 ? ReviewLight.Amber : ReviewLight.Green;
    }

    private static async Task<Ok<ReviewQueue>> ReviewsAsync(
        JobsDbContext db, IQuoteVersions quotes, IUserDirectory users, TimeProvider clock, CancellationToken cancellationToken)
    {
        var jobs = await db.Jobs.AsNoTracking()
            .Where(j => j.Status == JobStatus.InReview || j.Status == JobStatus.PendingEmail)
            .OrderBy(j => j.ReviewDueOn).ThenBy(j => j.CreatedAt)
            .ToListAsync(cancellationToken);
        var clients = (await users.FindManyAsync([.. jobs.Select(j => j.ClientId).Distinct()], cancellationToken)).ToDictionary(u => u.Id);
        var now = clock.GetUtcNow();
        var items = new List<ReviewItem>();
        foreach (var job in jobs)
        {
            var quote = await quotes.GetAsync(job.CurrentQuoteVersionId, cancellationToken);
            items.Add(new ReviewItem(
                job.Id, job.Code, job.Status, clients.GetValueOrDefault(job.ClientId)?.Name ?? "", job.Municipality, quote?.AreaM2 ?? 0,
                quote?.RangeMinCents, quote?.RangeMaxCents, quote?.OverMax ?? false, job.IsPremium, job.CreatedAt, job.ReviewDueOn, Light(job, now)));
        }

        return TypedResults.Ok(new ReviewQueue(
            [.. items.Where(i => i.Status == JobStatus.InReview)],
            [.. items.Where(i => i.Status == JobStatus.PendingEmail).OrderBy(i => i.CreatedAt)]));
    }

    private static async Task<Results<Ok<Paged<AdminJobListItem>>, ProblemHttpResult>> ListAsync(
        [AsParameters] AdminJobsQuery query, JobsDbContext db, IQuoteVersions quotes, IUserDirectory users, CancellationToken cancellationToken)
    {
        var (filtered, problem) = await FilterAsync(query, db, users, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var (page, pageSize) = Paged<AdminJobListItem>.Normalize(query.Page, query.PageSize);
        var total = await filtered!.CountAsync(cancellationToken);
        var jobs = await filtered!.OrderByDescending(j => j.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return TypedResults.Ok(new Paged<AdminJobListItem>(await ToListItemsAsync(jobs, quotes, users, cancellationToken), page, pageSize, total));
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> ExportAsync(
        [AsParameters] AdminJobsQuery query, JobsDbContext db, IQuoteVersions quotes, IUserDirectory users, CancellationToken cancellationToken)
    {
        var (filtered, problem) = await FilterAsync(query, db, users, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var jobs = await filtered!.OrderByDescending(j => j.UpdatedAt).Take(10_000).ToListAsync(cancellationToken);
        var csv = new StringBuilder("code,status,client,email,locality,municipality,total_eur,margin_eur,premium,created_at,updated_at\n");
        foreach (var j in await ToListItemsAsync(jobs, quotes, users, cancellationToken))
        {
            csv.AppendJoin(',',
                j.Code, JsonNamingPolicy.SnakeCaseLower.ConvertName(j.Status.ToString()), Csv(j.ClientName), Csv(j.ClientEmail), Csv(j.Locality),
                Csv(j.Municipality), (j.TotalCents / 100m).ToString("0.00", CultureInfo.InvariantCulture),
                (j.MarginCents / 100m).ToString("0.00", CultureInfo.InvariantCulture), j.Premium ? "yes" : "no",
                j.CreatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), j.UpdatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            csv.Append('\n');
        }

        // UTF-8 with BOM so spreadsheet apps read the accents.
        return TypedResults.File([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())], "text/csv", "trabajos.csv");
    }

    private static async Task<(IQueryable<Job>? Jobs, ProblemHttpResult? Problem)> FilterAsync(
        AdminJobsQuery query, JobsDbContext db, IUserDirectory users, CancellationToken cancellationToken)
    {
        var jobs = db.Jobs.AsNoTracking();
        if (query.Status is { Length: > 0 })
        {
            var statuses = new List<JobStatus>();
            foreach (var value in query.Status)
            {
                if (!Enum.TryParse<JobStatus>(value.Replace("_", ""), ignoreCase: true, out var status) || value.Any(char.IsUpper))
                {
                    return (null, ApiProblem.Validation(new Dictionary<string, string[]> { ["status"] = [$"Unknown status '{value}'."] }));
                }

                statuses.Add(status);
            }

            jobs = jobs.Where(j => statuses.Contains(j.Status));
        }

        if (query.From is { } from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), BusinessDays.Lisbon.GetUtcOffset(from.ToDateTime(TimeOnly.MinValue)));
            jobs = jobs.Where(j => j.CreatedAt >= start);
        }

        if (query.To is { } to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var endAt = new DateTimeOffset(end, BusinessDays.Lisbon.GetUtcOffset(end));
            jobs = jobs.Where(j => j.CreatedAt < endAt);
        }

        if (!string.IsNullOrWhiteSpace(query.Municipality))
        {
            jobs = jobs.Where(j => j.Municipality == query.Municipality);
        }

        if (query.Premium is { } premium)
        {
            jobs = jobs.Where(j => j.IsPremium == premium);
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            var clientIds = await users.SearchAsync(term, cancellationToken);
            jobs = jobs.Where(j => EF.Functions.ILike(j.Code, $"%{term}%") || clientIds.Contains(j.ClientId));
        }

        return (jobs, null);
    }

    private static async Task<IReadOnlyList<AdminJobListItem>> ToListItemsAsync(
        List<Job> jobs, IQuoteVersions quotes, IUserDirectory users, CancellationToken cancellationToken)
    {
        var clients = (await users.FindManyAsync([.. jobs.Select(j => j.ClientId).Distinct()], cancellationToken)).ToDictionary(u => u.Id);
        var totals = (await quotes.GetTotalsAsync([.. jobs.Select(j => j.CurrentQuoteVersionId)], cancellationToken)).ToDictionary(t => t.VersionId);
        return jobs.Select(j =>
        {
            var client = clients.GetValueOrDefault(j.ClientId);
            var total = totals.GetValueOrDefault(j.CurrentQuoteVersionId);
            // Painter and scheduled date arrive with assignments in H6.
            return new AdminJobListItem(j.Id, j.Code, j.Status, client?.Name ?? "", client?.Email ?? "", j.Locality, j.Municipality,
                total?.TotalCents ?? 0, total?.MarginCents ?? 0, j.IsPremium, null, null, j.CreatedAt, j.UpdatedAt);
        }).ToList();
    }

    private static string Csv(string value)
    {
        // A leading = + - @ would run as a formula in a spreadsheet.
        if (value.Length > 0 && "=+-@".Contains(value[0]))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    private static async Task<Results<Ok<AdminJobDetail>, ProblemHttpResult>> DetailAsync(
        Guid id, JobsDbContext db, IQuoteVersions quotes, IUserDirectory users, IMediaService media, IEmailLog emails, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var job = await db.Jobs.AsNoTracking().Include(j => j.History).SingleOrDefaultAsync(j => j.Id == id, cancellationToken);
        var current = job is null ? null : await quotes.GetAdminAsync(job.CurrentQuoteVersionId, cancellationToken);
        if (job is null || current is null)
        {
            return JobProblems.NotFound();
        }

        var people = (await users.FindManyAsync(
            [.. job.History.Where(h => h.UserId is not null).Select(h => h.UserId!.Value).Append(job.ClientId).Concat(job.SiteVisitAssigneeId is { } a ? [a] : []).Distinct()],
            cancellationToken)).ToDictionary(u => u.Id);
        var client = people[job.ClientId];
        return TypedResults.Ok(new AdminJobDetail(
            job.Id,
            job.Code,
            job.Status,
            job.Version,
            new ClientInfo(client.Id, client.Name, client.Email, client.Phone, client.Locale, client.EmailConfirmed),
            new AddressDto(job.AddressLine1, job.Floor, job.PostalCode, job.Locality, job.Municipality, job.AccessNotes),
            new PreferredDatesDto(job.PreferredAsap, job.PreferredFrom, job.PreferredTo),
            job.Description,
            job.TaxId,
            job.CompanyName,
            job.IsPremium,
            job.ReviewDueOn,
            Light(job, clock.GetUtcNow()),
            job.QuoteValidUntil,
            job.SiteVisitAt is { } at
                ? new SiteVisitDto(at, job.SiteVisitAssigneeId!.Value, people.GetValueOrDefault(job.SiteVisitAssigneeId.Value)?.Name ?? "", Money.ToCents(job.SiteVisitPrice ?? 0))
                : null,
            current,
            await quotes.GetDraftAsync(job.Id, cancellationToken),
            await quotes.ListAsync(job.Id, cancellationToken),
            await media.ListForJobAsync(job.Id, cancellationToken),
            [.. job.History.OrderBy(h => h.ChangedAt).Select(h =>
                new JobHistoryEntry(h.From, h.To, h.Actor, h.UserId is { } u ? people.GetValueOrDefault(u)?.Name : null, h.Reason, h.ChangedAt))],
            await emails.ListForJobAsync(job.Id, cancellationToken),
            AllowedTransitions(job.Status),
            job.CreatedAt,
            job.UpdatedAt));
    }

    /// <summary>What the admin may move the job to (A3.3): only transitions of docs/states.md the admin performs.</summary>
    public static IReadOnlyList<JobStatus> AllowedTransitions(JobStatus status) =>
        [.. JobStateMachine.Transitions.Where(t => t.From == status && t.Actors.Contains(JobActor.Admin)).Select(t => t.To).Distinct()];

    private static async Task<Ok<IReadOnlyList<PartnerDto>>> PartnersAsync(IUserDirectory users, CancellationToken cancellationToken) =>
        TypedResults.Ok<IReadOnlyList<PartnerDto>>([.. (await users.ListAdminsAsync(cancellationToken)).Select(u => new PartnerDto(u.Id, u.Name, u.Email))]);
}
