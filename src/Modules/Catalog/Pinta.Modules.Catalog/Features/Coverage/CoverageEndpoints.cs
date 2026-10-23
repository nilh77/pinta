using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Data;
using Pinta.Modules.Catalog.Domain;
using Pinta.Modules.Catalog.Resources;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Data;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Catalog.Features.Coverage;

public sealed record WaitlistEntryDto(Guid Id, string Email, string PostalCode, Locale Locale, DateTimeOffset CreatedAt);

public sealed record CoverageAreaDto(Guid Id, string PostalPrefix, string Municipality, bool IsActive, uint Version, DateTimeOffset UpdatedAt);

/// <summary>The prefix is set on create and ignored on edit; edits carry the version they started from.</summary>
public sealed record CoverageAreaRequest(string? PostalPrefix, string Municipality, bool IsActive, uint? Version);

internal sealed class CoverageAreaRequestValidator : AbstractValidator<CoverageAreaRequest>
{
    public CoverageAreaRequestValidator() => RuleFor(r => r.Municipality).NotEmpty().MaximumLength(100);
}

/// <summary>GET / POST / PUT /api/admin/coverage — postal prefixes and concelhos served (A1.7). No delete: deactivate.</summary>
internal static class CoverageEndpoints
{
    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapGet("/coverage", ListAsync).WithName("ListCoverageAreas");
        admin.MapPost("/coverage", CreateAsync).WithValidation<CoverageAreaRequest>().WithName("CreateCoverageArea");
        admin.MapPut("/coverage/{id:guid}", UpdateAsync).WithValidation<CoverageAreaRequest>().WithName("UpdateCoverageArea")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapGet("/coverage/waitlist", WaitlistAsync).WithName("ListWaitlist");
        admin.MapGet("/coverage/waitlist/export", ExportWaitlistAsync).WithName("ExportWaitlist").Produces<string>(StatusCodes.Status200OK, "text/csv");
    }

    /// <summary>Who asked to hear when Pinta reaches their postal code, newest first (screen Cobertura).</summary>
    private static async Task<Ok<WaitlistEntryDto[]>> WaitlistAsync(CatalogDbContext db, CancellationToken cancellationToken) =>
        TypedResults.Ok(await db.WaitlistEntries.AsNoTracking()
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WaitlistEntryDto(w.Id, w.Email, w.PostalCode, w.Locale, w.CreatedAt))
            .ToArrayAsync(cancellationToken));

    private static async Task<FileContentHttpResult> ExportWaitlistAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        var rows = await db.WaitlistEntries.AsNoTracking().OrderByDescending(w => w.CreatedAt).ToListAsync(cancellationToken);
        var csv = new System.Text.StringBuilder("email,postal_code,locale,created_at\n");
        foreach (var w in rows)
        {
            csv.Append($"{Pinta.SharedKernel.Text.Csv.Field(w.Email)},{w.PostalCode},{w.Locale.ToString().ToLowerInvariant()},{w.CreatedAt.UtcDateTime:O}\n");
        }

        return TypedResults.File(
            [.. System.Text.Encoding.UTF8.GetPreamble(), .. System.Text.Encoding.UTF8.GetBytes(csv.ToString())], "text/csv", "lista-de-espera.csv");
    }

    private static async Task<Ok<CoverageAreaDto[]>> ListAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        var areas = await db.CoverageAreas.AsNoTracking()
            .OrderBy(a => a.Municipality).ThenBy(a => a.PostalPrefix)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(areas.Select(ToDto).ToArray());
    }

    private static async Task<Results<Created<CoverageAreaDto>, ProblemHttpResult>> CreateAsync(
        CoverageAreaRequest request, CatalogDbContext db, TimeProvider clock, HttpContext http, CancellationToken cancellationToken)
    {
        if (request.PostalPrefix is null || request.PostalPrefix.Length != 4 || !request.PostalPrefix.All(char.IsAsciiDigit))
        {
            return Invalid("postalPrefix", "prefix_format");
        }

        if (await db.CoverageAreas.AnyAsync(a => a.PostalPrefix == request.PostalPrefix, cancellationToken))
        {
            return Invalid("postalPrefix", "prefix_taken");
        }

        var area = new CoverageArea
        {
            PostalPrefix = request.PostalPrefix,
            Municipality = request.Municipality.Trim(),
            IsActive = request.IsActive,
        };
        area.Stamp(clock.GetUtcNow(), http.User.GetUserId());
        db.CoverageAreas.Add(area);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/admin/coverage/{area.Id}", ToDto(area));
    }

    private static async Task<Results<Ok<CoverageAreaDto>, ProblemHttpResult>> UpdateAsync(
        Guid id, CoverageAreaRequest request, CatalogDbContext db, TimeProvider clock, HttpContext http, CancellationToken cancellationToken)
    {
        var area = await db.CoverageAreas.FindAsync([id], cancellationToken);
        if (area is null)
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        if (request.Version is null)
        {
            return Invalid("version", "version_required");
        }

        area.Municipality = request.Municipality.Trim();
        area.IsActive = request.IsActive;
        area.Stamp(clock.GetUtcNow(), http.User.GetUserId());
        if (!await db.TrySaveVersionAsync(area, request.Version.Value, cancellationToken))
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict);
        }

        return TypedResults.Ok(ToDto(area));
    }

    private static ProblemHttpResult Invalid(string field, string message) =>
        ApiProblem.Validation(new Dictionary<string, string[]> { [field] = [CatalogMessages.Get(message)] });

    private static CoverageAreaDto ToDto(CoverageArea a) => new(a.Id, a.PostalPrefix, a.Municipality, a.IsActive, a.Version, a.UpdatedAt);
}
