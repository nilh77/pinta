using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Contracts;
using Pinta.Modules.Media.Contracts;
using Pinta.Modules.Media.Data;
using Pinta.Modules.Media.Domain;
using Pinta.Modules.Media.Storage;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Media.Features.Uploads;

/// <summary>
/// Client photos and videos before the request is sent. <see cref="DraftId"/> is the random id of the builder's draft;
/// <see cref="ElementKey"/> is the element's id in the draft, or null for the general photos.
/// </summary>
public sealed record UploadUrlRequest(Guid DraftId, string? ElementKey, MediaKind Kind, string ContentType, long SizeBytes);

public sealed record UploadUrlResponse(Guid FileId, string Url, IReadOnlyDictionary<string, string> Fields, DateTimeOffset ExpiresAt);

public sealed record DraftFileRequest(Guid DraftId);

public sealed record MediaFileDto(Guid Id, string? ElementKey, MediaKind Kind, string ContentType, long SizeBytes, bool Confirmed);

internal sealed class UploadUrlRequestValidator : AbstractValidator<UploadUrlRequest>
{
    public UploadUrlRequestValidator()
    {
        RuleFor(r => r.DraftId).NotEmpty();
        RuleFor(r => r.ElementKey).MaximumLength(64);
        RuleFor(r => r.Kind).Must(k => k is MediaKind.Image or MediaKind.Video);
        RuleFor(r => r.ContentType).NotEmpty().MaximumLength(64);
        RuleFor(r => r.SizeBytes).GreaterThan(0);
    }
}

/// <summary>POST /api/media/upload-url, POST /api/media/{fileId}/confirm, DELETE /api/media/{fileId} (C3.1).</summary>
internal static class UploadEndpoints
{
    /// <summary>Types the client may upload (docs/screens.md, screen 5) and their file extension.</summary>
    public static readonly IReadOnlyDictionary<string, (MediaKind Kind, string Extension)> AllowedTypes = new Dictionary<string, (MediaKind, string)>
    {
        ["image/jpeg"] = (MediaKind.Image, "jpg"),
        ["image/png"] = (MediaKind.Image, "png"),
        ["image/webp"] = (MediaKind.Image, "webp"),
        ["image/heic"] = (MediaKind.Image, "heic"),
        ["image/heif"] = (MediaKind.Image, "heif"),
        ["video/mp4"] = (MediaKind.Video, "mp4"),
        ["video/quicktime"] = (MediaKind.Video, "mov"),
    };

    private static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(10);
    private const long Megabyte = 1024 * 1024;

    public static void Map(IEndpointRouteBuilder app)
    {
        var media = app.MapGroup("/api/media").WithTags("Media");
        media.MapPost("/upload-url", CreateUploadAsync)
            .WithValidation<UploadUrlRequest>()
            .RequireRateLimiting(RateLimitPolicies.Uploads)
            .WithName("CreateUploadUrl")
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        media.MapPost("/{fileId:guid}/confirm", ConfirmAsync).WithName("ConfirmUpload").ProducesProblem(StatusCodes.Status404NotFound);
        media.MapDelete("/{fileId:guid}", DeleteAsync).WithName("DeleteUpload").ProducesProblem(StatusCodes.Status404NotFound);
        ReadUrlEndpoint.Map(media);
    }

    private static async Task<Results<Ok<UploadUrlResponse>, ProblemHttpResult>> CreateUploadAsync(
        UploadUrlRequest request, MediaDbContext db, ObjectStorage storage, IUploadLimits limitsProvider, TimeProvider clock,
        HttpContext http, CancellationToken cancellationToken)
    {
        var contentType = request.ContentType.Trim().ToLowerInvariant();
        if (!AllowedTypes.TryGetValue(contentType, out var type) || type.Kind != request.Kind)
        {
            return ApiProblem.Create(StatusCodes.Status415UnsupportedMediaType, ErrorCodes.UnsupportedMediaType);
        }

        var limits = await limitsProvider.GetAsync(cancellationToken);
        var maxBytes = (request.Kind == MediaKind.Video ? limits.MaxVideoSizeMb : limits.MaxImageSizeMb) * Megabyte;
        if (request.SizeBytes > maxBytes)
        {
            return ApiProblem.Create(StatusCodes.Status413PayloadTooLarge, ErrorCodes.FileTooLarge);
        }

        var elementKey = string.IsNullOrWhiteSpace(request.ElementKey) ? null : request.ElementKey.Trim();
        var existing = await db.Files.CountAsync(
            f => f.DraftId == request.DraftId && f.JobId == null && f.ElementKey == elementKey && f.Kind == request.Kind,
            cancellationToken);
        var maxCount = request.Kind == MediaKind.Video ? limits.MaxVideosPerElement : limits.MaxPhotosPerElement;
        if (existing >= maxCount)
        {
            return ApiProblem.Create(StatusCodes.Status422UnprocessableEntity, ErrorCodes.MediaLimitReached);
        }

        var file = new MediaFile
        {
            DraftId = request.DraftId,
            UploadedBy = http.User.GetUserId(),
            ElementKey = elementKey,
            Context = elementKey is null ? MediaContext.JobGeneral : MediaContext.Element,
            Kind = request.Kind,
            ContentType = contentType,
            SizeBytes = request.SizeBytes,
            StorageKey = $"uploads/{request.DraftId:N}/{Guid.CreateVersion7():N}.{type.Extension}",
            CreatedAt = clock.GetUtcNow(),
        };
        await storage.EnsureBucketAsync(cancellationToken);
        var upload = storage.CreateUpload(file.StorageKey, contentType, maxBytes, UploadLifetime);
        db.Files.Add(file);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new UploadUrlResponse(file.Id, upload.Url, upload.Fields, upload.ExpiresAt));
    }

    /// <summary>Confirms the upload once the object is in storage with the declared type (S3 enforced the size).</summary>
    private static async Task<Results<Ok<MediaFileDto>, ProblemHttpResult>> ConfirmAsync(
        Guid fileId, DraftFileRequest request, MediaDbContext db, ObjectStorage storage, TimeProvider clock, CancellationToken cancellationToken)
    {
        var file = await FindDraftFileAsync(db, fileId, request.DraftId, cancellationToken);
        if (file is null)
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        if (!file.Confirmed)
        {
            var stored = await storage.FindAsync(file.StorageKey, cancellationToken);
            if (stored is null || !string.Equals(stored.ContentType, file.ContentType, StringComparison.OrdinalIgnoreCase))
            {
                return ApiProblem.Validation(new Dictionary<string, string[]> { ["file"] = ["The file has not been uploaded."] });
            }

            file.SizeBytes = stored.SizeBytes;
            file.Confirmed = true;
            file.ConfirmedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(new MediaFileDto(file.Id, file.ElementKey, file.Kind, file.ContentType, file.SizeBytes, file.Confirmed));
    }

    /// <summary>The client removes a file before sending the request (docs/screens.md, screen 5).</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid fileId, Guid draftId, MediaDbContext db, ObjectStorage storage, CancellationToken cancellationToken)
    {
        var file = await FindDraftFileAsync(db, fileId, draftId, cancellationToken);
        if (file is null)
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        await storage.DeleteAsync(file.StorageKey, cancellationToken);
        db.Files.Remove(file);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Only whoever holds the draft id can touch its files, and only before they belong to a job.</summary>
    private static Task<MediaFile?> FindDraftFileAsync(MediaDbContext db, Guid fileId, Guid draftId, CancellationToken cancellationToken) =>
        db.Files.SingleOrDefaultAsync(f => f.Id == fileId && f.DraftId == draftId && f.JobId == null, cancellationToken);
}
