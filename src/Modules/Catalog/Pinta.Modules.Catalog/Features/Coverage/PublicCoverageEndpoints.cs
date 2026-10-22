using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pinta.Modules.Catalog.Contracts;
using Pinta.Modules.Catalog.Data;
using Pinta.Modules.Catalog.Domain;
using Pinta.Modules.Catalog.Resources;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Catalog.Features.Coverage;

internal sealed class CoverageService(CatalogDbContext db) : ICoverageService
{
    public async Task<string?> FindMunicipalityAsync(string postalCode, CancellationToken cancellationToken = default)
    {
        if (!Regex.IsMatch(postalCode, PostalCode.Pattern))
        {
            return null;
        }

        var prefix = postalCode[..4];
        return await db.CoverageAreas.AsNoTracking()
            .Where(a => a.IsActive && a.PostalPrefix == prefix)
            .Select(a => a.Municipality)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public sealed record CoverageCheckResponse(bool Covered, string? Municipality);

public sealed record WaitlistRequest(string Email, string PostalCode, Locale Locale);

internal sealed class WaitlistRequestValidator : AbstractValidator<WaitlistRequest>
{
    public WaitlistRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.PostalCode).NotEmpty().Matches(PostalCode.Pattern).WithMessage(_ => CatalogMessages.Get("postal_code_format"));
        RuleFor(r => r.Locale).IsInEnum();
    }
}

/// <summary>GET /api/coverage/check and POST /api/coverage/waitlist — public, before the client has an account (C4.1).</summary>
internal static class PublicCoverageEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var coverage = app.MapGroup("/api/coverage").WithTags("Coverage");
        coverage.MapGet("/check", CheckAsync).WithName("CheckCoverage");
        coverage.MapPost("/waitlist", JoinWaitlistAsync)
            .WithValidation<WaitlistRequest>()
            .RequireRateLimiting(RateLimitPolicies.PublicForm)
            .WithName("JoinWaitlist");
    }

    private static async Task<Results<Ok<CoverageCheckResponse>, ProblemHttpResult>> CheckAsync(
        string postalCode, ICoverageService coverage, CancellationToken cancellationToken)
    {
        postalCode = postalCode.Trim();
        if (!Regex.IsMatch(postalCode, PostalCode.Pattern))
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["postalCode"] = [CatalogMessages.Get("postal_code_format")] });
        }

        var municipality = await coverage.FindMunicipalityAsync(postalCode, cancellationToken);
        return TypedResults.Ok(new CoverageCheckResponse(municipality is not null, municipality));
    }

    private static async Task<NoContent> JoinWaitlistAsync(
        WaitlistRequest request, CatalogDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        db.WaitlistEntries.Add(new WaitlistEntry
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            PostalCode = request.PostalCode,
            Locale = request.Locale,
            CreatedAt = clock.GetUtcNow(),
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Already on the list for this postal code: same answer, nothing new to store.
        }

        return TypedResults.NoContent();
    }
}
