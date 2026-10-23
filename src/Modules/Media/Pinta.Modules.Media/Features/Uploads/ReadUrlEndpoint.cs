using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Media.Data;
using Pinta.Modules.Media.Storage;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Media.Features.Uploads;

public sealed record ReadUrlResponse(string Url, DateTimeOffset ExpiresAt);

/// <summary>GET /api/media/{fileId}/url — signed read URL for admins and the job's client; anyone else gets 404 (C5.5).</summary>
internal static class ReadUrlEndpoint
{
    public static void Map(RouteGroupBuilder media) =>
        media.MapGet("/{fileId:guid}/url", HandleAsync)
            .RequireAuthorization()
            .WithName("GetMediaUrl")
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<Ok<ReadUrlResponse>, ProblemHttpResult>> HandleAsync(
        Guid fileId, ClaimsPrincipal user, MediaDbContext db, ObjectStorage storage, IJobAccess jobs, CancellationToken cancellationToken)
    {
        var file = await db.Files.AsNoTracking().SingleOrDefaultAsync(f => f.Id == fileId && f.Confirmed && f.JobId != null, cancellationToken);
        var userId = user.GetUserId();
        if (file is null || userId is null
            || !(user.IsInRole(RoleNames.Admin) || await jobs.IsClientOfAsync(file.JobId!.Value, userId.Value, cancellationToken)))
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        var (url, expiresAt) = storage.CreateDownload(file.StorageKey, MediaService.ReadUrlLifetime);
        return TypedResults.Ok(new ReadUrlResponse(url, expiresAt));
    }
}
