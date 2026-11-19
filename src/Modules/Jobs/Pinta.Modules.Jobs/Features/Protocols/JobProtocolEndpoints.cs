using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Domain;
using Pinta.Modules.Jobs.Features.Shared;
using Pinta.Modules.Media.Contracts;
using Pinta.Modules.Painters.Contracts;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Jobs.Features.Protocols;

public enum ProtocolStatus
{
    Pending,
    Uploaded,
    ReceivedInPerson,
}

public sealed record JobProtocolReceipt(
    ProtocolStatus Status, DateTimeOffset? ReceivedAt, string? ReceivedBy, bool CanReceive, IReadOnlyList<StoredFile> Files);

public sealed record ProtocolUploadRequest(string ContentType, long SizeBytes);
public sealed record ProtocolSubmission(IReadOnlyList<Guid> FileIds);

internal sealed class ProtocolUploadValidator : AbstractValidator<ProtocolUploadRequest>
{
    public ProtocolUploadValidator()
    {
        RuleFor(request => request.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(request => request.SizeBytes).GreaterThan(0);
    }
}

internal sealed class ProtocolSubmissionValidator : AbstractValidator<ProtocolSubmission>
{
    public ProtocolSubmissionValidator() => RuleFor(request => request.FileIds).NotEmpty()
        .Must(ids => ids is { Count: <= 4 } && ids.Distinct().Count() == ids.Count);
}

internal static class JobProtocolEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/jobs/{id:guid}/protocol").WithTags("Admin jobs")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
        var painter = app.MapGroup("/api/painter/jobs/{id:guid}/protocol").WithTags("Painter")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Painter));

        admin.MapGet("", GetAsync).WithName("GetAdminJobProtocol");
        painter.MapGet("", GetAsync).WithName("GetPainterJobProtocol");
        admin.MapGet("/pdf", DownloadAsync).WithName("DownloadAdminJobProtocol")
            .Produces<byte[]>(StatusCodes.Status200OK, "application/pdf").ProducesProblem(StatusCodes.Status404NotFound);
        painter.MapGet("/pdf", DownloadAsync).WithName("DownloadPainterJobProtocol")
            .Produces<byte[]>(StatusCodes.Status200OK, "application/pdf").ProducesProblem(StatusCodes.Status404NotFound);

        painter.MapPost("/upload-url", CreateUploadAsync).WithName("CreateJobProtocolUpload")
            .WithValidation<ProtocolUploadRequest>().RequireRateLimiting(RateLimitPolicies.Uploads)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge).ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        painter.MapPost("/submit", SubmitAsync).WithName("SubmitJobProtocol").WithValidation<ProtocolSubmission>();
        admin.MapPost("/receive-in-person", ReceiveInPersonAsync).WithName("ReceiveJobProtocolInPerson");
    }

    private static async Task<Results<Ok<JobProtocolReceipt>, ProblemHttpResult>> GetAsync(
        Guid id, HttpContext http, JobsDbContext db, IPainterDirectory painters, IMediaService media, CancellationToken ct)
    {
        var protocol = await FindAsync(id, http, db, painters, lockForReceipt: false, ct);
        if (protocol is null)
        {
            return JobProblems.NotFound();
        }

        http.Response.Headers.CacheControl = "private, no-store";
        return TypedResults.Ok(await ReceiptAsync(protocol, await WorkFinishedAsync(id, db, ct), media, ct));
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> DownloadAsync(
        Guid id, HttpContext http, JobsDbContext db, IPainterDirectory painters, CancellationToken ct)
    {
        var protocol = await FindAsync(id, http, db, painters, lockForReceipt: false, ct);
        if (protocol is null)
        {
            return JobProblems.NotFound();
        }

        http.Response.Headers.CacheControl = "private, no-store";
        return TypedResults.File(protocol.Pdf, "application/pdf", $"Pinta-{protocol.JobCode}.pdf");
    }

    private static async Task<Results<Ok<SignedUpload>, ProblemHttpResult>> CreateUploadAsync(
        Guid id, ProtocolUploadRequest request, HttpContext http, JobsDbContext db, IPainterDirectory painters, IMediaService media, CancellationToken ct)
    {
        var protocol = await FindAsync(id, http, db, painters, lockForReceipt: false, ct);
        if (protocol is null)
        {
            return JobProblems.NotFound();
        }
        if (protocol.ReceivedAt is not null)
        {
            return JobProblems.ConcurrencyConflict();
        }
        if (!await WorkFinishedAsync(id, db, ct))
        {
            return JobProblems.InvalidTransition();
        }

        var (upload, refusal) = await media.CreateOwnedUploadAsync(new OwnedUploadRequest(
            MediaContext.SignedProtocol, http.User.GetUserId()!.Value, id, null, request.ContentType, request.SizeBytes), ct);
        return refusal switch
        {
            UploadRefusal.UnsupportedType => ApiProblem.Create(StatusCodes.Status415UnsupportedMediaType, ErrorCodes.UnsupportedMediaType),
            UploadRefusal.TooLarge => ApiProblem.Create(StatusCodes.Status413PayloadTooLarge, ErrorCodes.FileTooLarge),
            _ => TypedResults.Ok(upload!),
        };
    }

    private static Task<Results<Ok<JobProtocolReceipt>, ProblemHttpResult>> SubmitAsync(
        Guid id, ProtocolSubmission request, HttpContext http, JobsDbContext db, IPainterDirectory painters,
        IMediaService media, IUserDirectory users, TimeProvider clock, CancellationToken ct) =>
        ReceiveAsync(id, [.. request.FileIds], http, db, painters, media, users, clock, ct);

    private static Task<Results<Ok<JobProtocolReceipt>, ProblemHttpResult>> ReceiveInPersonAsync(
        Guid id, HttpContext http, JobsDbContext db, IPainterDirectory painters, IMediaService media, IUserDirectory users,
        TimeProvider clock, CancellationToken ct) =>
        ReceiveAsync(id, [], http, db, painters, media, users, clock, ct);

    private static async Task<Results<Ok<JobProtocolReceipt>, ProblemHttpResult>> ReceiveAsync(
        Guid id, Guid[] fileIds, HttpContext http, JobsDbContext db, IPainterDirectory painters, IMediaService media,
        IUserDirectory users, TimeProvider clock, CancellationToken ct)
    {
        // Serialize the two receipt channels before confirming files in the Media module.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var protocol = await FindAsync(id, http, db, painters, lockForReceipt: true, ct);
        if (protocol is null)
        {
            return JobProblems.NotFound();
        }

        http.Response.Headers.CacheControl = "private, no-store";
        if (protocol.ReceivedAt is not null)
        {
            if (!protocol.FileIds.ToHashSet().SetEquals(fileIds))
            {
                return JobProblems.ConcurrencyConflict();
            }

            return TypedResults.Ok(await ReceiptAsync(protocol, finished: true, media, ct));
        }
        if (!await WorkFinishedAsync(id, db, ct))
        {
            return JobProblems.InvalidTransition();
        }

        var userId = http.User.GetUserId()!.Value;
        var user = await users.FindAsync(userId, ct);
        if (user is null)
        {
            return JobProblems.NotFound();
        }

        var files = fileIds.Length == 0
            ? []
            : await media.ConfirmOwnedAsync(fileIds, userId, id, null, MediaContext.SignedProtocol, ct);
        if (files.Count != fileIds.Length)
        {
            return ApiProblem.Create(StatusCodes.Status422UnprocessableEntity, ErrorCodes.ValidationFailed);
        }

        protocol.RecordReceipt(userId, user.Name, clock.GetUtcNow(), fileIds);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Ok(ToReceipt(protocol, finished: true, files));
    }

    private static async Task<JobProtocol?> FindAsync(
        Guid id, HttpContext http, JobsDbContext db, IPainterDirectory painters, bool lockForReceipt, CancellationToken ct)
    {
        Guid? painterId = null;
        if (!http.User.IsInRole(RoleNames.Admin))
        {
            var painter = await painters.FindByUserAsync(http.User.GetUserId()!.Value, ct);
            if (painter is null)
            {
                return null;
            }
            painterId = painter.Id;
        }

        if (lockForReceipt)
        {
            var rows = await db.Protocols.FromSql($"""
                SELECT *, xmin FROM jobs.job_protocols
                WHERE job_id = {id} AND ({painterId}::uuid IS NULL OR painter_id = {painterId}::uuid)
                FOR UPDATE
                """).ToListAsync(ct);
            return rows.SingleOrDefault();
        }

        return await db.Protocols.AsNoTracking().SingleOrDefaultAsync(
            p => p.JobId == id && (!painterId.HasValue || p.PainterId == painterId), ct);
    }

    private static Task<bool> WorkFinishedAsync(Guid jobId, JobsDbContext db, CancellationToken ct) =>
        db.Jobs.AnyAsync(j => j.Id == jobId && (j.Status == JobStatus.Finished || j.Status == JobStatus.Closed), ct);

    private static async Task<JobProtocolReceipt> ReceiptAsync(JobProtocol protocol, bool finished, IMediaService media, CancellationToken ct) =>
        ToReceipt(protocol, finished, protocol.FileIds.Length == 0 ? [] : await media.FindAsync(protocol.FileIds, ct));

    private static JobProtocolReceipt ToReceipt(JobProtocol protocol, bool finished, IReadOnlyList<StoredFile> files)
    {
        var status = protocol.ReceivedAt is null ? ProtocolStatus.Pending
            : protocol.FileIds.Length == 0 ? ProtocolStatus.ReceivedInPerson : ProtocolStatus.Uploaded;
        return new JobProtocolReceipt(status, protocol.ReceivedAt, protocol.ReceivedBy, finished && status == ProtocolStatus.Pending, files);
    }
}
