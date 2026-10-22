using System.Text.RegularExpressions;
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

namespace Pinta.Modules.Catalog.Features.Admin;

/// <summary>Body of a create (with <see cref="Key"/>) or an edit (with the <see cref="Version"/> being edited).</summary>
public interface ICatalogItemRequest
{
    string? Key { get; }

    uint? Version { get; }
}

/// <summary>Extra checks against the database; returns field errors, or none.</summary>
internal delegate Task<Dictionary<string, string[]>> CatalogCheck<in TEntity, in TRequest>(
    CatalogDbContext db, TRequest request, TEntity? existing, CancellationToken cancellationToken);

/// <summary>
/// List, create and edit for one catalog list (A1.1–A1.5). No delete: items are deactivated. Edits carry the version
/// they started from; a stale one is <c>409 concurrency_conflict</c>.
/// </summary>
internal sealed record CatalogCrud<TEntity, TRequest, TDto>(
    string Path,
    string Name,
    string PluralName,
    Func<TEntity, TDto> ToDto,
    Func<TRequest, TEntity> Create, // the new entity with its key; Apply sets the rest
    Action<TRequest, TEntity> Apply,
    CatalogCheck<TEntity, TRequest>? Check = null)
    where TEntity : CatalogEntity
    where TRequest : ICatalogItemRequest
    where TDto : notnull
{
    public void Map(RouteGroupBuilder group)
    {
        group.MapGet($"/{Path}", ListAsync).WithName($"List{PluralName}");
        group.MapPost($"/{Path}", CreateAsync).WithValidation<TRequest>().WithName($"Create{Name}");
        group.MapPut($"/{Path}/{{id:guid}}", UpdateAsync).WithValidation<TRequest>().WithName($"Update{Name}")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private async Task<Ok<TDto[]>> ListAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        var items = await db.Set<TEntity>().AsNoTracking()
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Key)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(items.Select(ToDto).ToArray());
    }

    private async Task<Results<Created<TDto>, ProblemHttpResult>> CreateAsync(
        TRequest request, CatalogDbContext db, TimeProvider clock, HttpContext http, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Key is null || !Regex.IsMatch(request.Key, CatalogRules.KeyPattern))
        {
            errors["key"] = [CatalogMessages.Get("key_format")];
        }
        else if (await db.Set<TEntity>().AnyAsync(e => e.Key == request.Key, cancellationToken))
        {
            errors["key"] = [CatalogMessages.Get("key_taken")];
        }

        if (Check is not null)
        {
            Merge(errors, await Check(db, request, null, cancellationToken));
        }

        if (errors.Count > 0)
        {
            return ApiProblem.Validation(errors);
        }

        var entity = Create(request);
        Apply(request, entity);
        entity.Stamp(clock.GetUtcNow(), http.User.GetUserId());
        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/admin/catalog/{Path}/{entity.Id}", ToDto(entity));
    }

    private async Task<Results<Ok<TDto>, ProblemHttpResult>> UpdateAsync(
        Guid id, TRequest request, CatalogDbContext db, TimeProvider clock, HttpContext http, CancellationToken cancellationToken)
    {
        var entity = await db.Set<TEntity>().FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        var errors = new Dictionary<string, string[]>();
        if (request.Version is null)
        {
            errors["version"] = [CatalogMessages.Get("version_required")];
        }

        if (Check is not null)
        {
            Merge(errors, await Check(db, request, entity, cancellationToken));
        }

        if (errors.Count > 0)
        {
            return ApiProblem.Validation(errors);
        }

        Apply(request, entity);
        entity.Stamp(clock.GetUtcNow(), http.User.GetUserId());
        if (!await db.TrySaveVersionAsync(entity, request.Version!.Value, cancellationToken))
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict);
        }

        return TypedResults.Ok(ToDto(entity));
    }

    private static void Merge(Dictionary<string, string[]> errors, Dictionary<string, string[]> more)
    {
        foreach (var (field, messages) in more)
        {
            errors[field] = errors.TryGetValue(field, out var existing) ? [.. existing, .. messages] : messages;
        }
    }
}
