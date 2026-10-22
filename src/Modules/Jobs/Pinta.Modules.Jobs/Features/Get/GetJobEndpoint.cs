using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Quoting.Contracts;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Jobs.Features.Get;

public sealed record PreferredDatesDto(bool Asap, DateOnly? From, DateOnly? To);

/// <summary>What the confirmation screen shows (screen 8); the full detail with timeline arrives in H4 (C5.2).</summary>
public sealed record JobSummaryResponse(
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
    DateOnly? ReviewDueOn);

/// <summary>GET /api/jobs/{id} — only the client who owns it; any other job is 404 (docs/api.md).</summary>
internal static class GetJobEndpoint
{
    public static void Map(RouteGroupBuilder jobs) =>
        jobs.MapGet("/{id:guid}", HandleAsync)
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Client))
            .WithName("GetJob")
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<Ok<JobSummaryResponse>, ProblemHttpResult>> HandleAsync(
        Guid id, HttpContext http, JobsDbContext db, IQuoteVersions quotes, CancellationToken cancellationToken)
    {
        var userId = http.User.GetUserId();
        var job = await db.Jobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id && j.ClientId == userId, cancellationToken);
        var quote = job is null ? null : await quotes.GetAsync(job.CurrentQuoteVersionId, cancellationToken);
        if (job is null || quote is null)
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        return TypedResults.Ok(new JobSummaryResponse(
            job.Id, job.Code, job.Status, job.CreatedAt, quote.AreaM2, quote.RangeMinCents, quote.RangeMaxCents, quote.OverMax,
            job.AddressLine1, job.PostalCode, job.Locality,
            new PreferredDatesDto(job.PreferredAsap, job.PreferredFrom, job.PreferredTo), job.ReviewDueOn));
    }
}
