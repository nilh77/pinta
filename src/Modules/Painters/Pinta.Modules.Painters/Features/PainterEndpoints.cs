using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Contracts;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Media.Contracts;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Painters.Contracts;
using Pinta.Modules.Painters.Data;
using Pinta.Modules.Painters.Domain;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Data;
using Pinta.SharedKernel.Http;
using Pinta.SharedKernel.Time;

namespace Pinta.Modules.Painters.Features;

public sealed record PainterListItem(
    Guid Id, string Name, string Email, string? Phone, PainterStatus Status, IReadOnlyList<string> Municipalities,
    IReadOnlyList<string> Specialties, decimal? Rating, int JobsDone, DateTimeOffset CreatedAt);

public sealed record PainterDocumentDto(Guid Id, PainterDocumentKind Kind, DateOnly? ExpiresOn, bool Expired, StoredFile? File);

/// <summary>The painter's page for the admin (A4.4). The IBAN only ever travels masked.</summary>
public sealed record PainterDetail(
    Guid Id,
    Guid UserId,
    string Name,
    string Email,
    string? Phone,
    string? TaxId,
    IReadOnlyList<string> Specialties,
    IReadOnlyList<string> Municipalities,
    string? IbanMasked,
    string? Notes,
    decimal? Rating,
    PainterStatus Status,
    bool SignedUp,
    IReadOnlyList<PainterDocumentDto> Documents,
    IReadOnlyList<PainterJob> Jobs,
    uint Version,
    DateTimeOffset CreatedAt);

/// <summary>A4.1: only name and email are required; the painter completes the rest when signing up.</summary>
public sealed record CreatePainterRequest(
    string Name, string Email, string? Phone, string? TaxId, IReadOnlyList<string>? Specialties, IReadOnlyList<string>? Municipalities,
    string? Iban, string? Notes, Locale? Locale);

/// <summary><see cref="Iban"/> null keeps the stored one.</summary>
public sealed record UpdatePainterRequest(
    string Name, string? Phone, string? TaxId, IReadOnlyList<string> Specialties, IReadOnlyList<string> Municipalities, string? Iban,
    string? Notes, decimal? Rating, uint Version);

public sealed record DocumentUploadRequest(string ContentType, long SizeBytes);

public sealed record AddDocumentRequest(Guid FileId, PainterDocumentKind Kind, DateOnly? ExpiresOn);

/// <summary>The painter's own profile (onboarding and screen P4).</summary>
public sealed record PainterProfile(
    string Name, string Email, string? Phone, string? TaxId, IReadOnlyList<string> Specialties, IReadOnlyList<string> Municipalities,
    string? IbanMasked, PainterStatus Status, IReadOnlyList<string> AvailableMunicipalities);

/// <summary>Changing a stored IBAN asks for <see cref="CurrentPassword"/>; setting the first one does not.</summary>
public sealed record UpdateProfileRequest(string? Phone, string? TaxId, IReadOnlyList<string> Municipalities, string? Iban, string? CurrentPassword);

internal static class PainterRules
{
    public static readonly string[] Specialties = ["interior", "exterior", "wood", "metal"];

    public const string PhonePattern = @"^\+[1-9]\d{7,14}$";
}

internal sealed class CreatePainterRequestValidator : AbstractValidator<CreatePainterRequest>
{
    public CreatePainterRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.Phone).Matches(PainterRules.PhonePattern).When(r => !string.IsNullOrWhiteSpace(r.Phone));
        RuleFor(r => r.TaxId).Must(TaxId.IsValid).When(r => !string.IsNullOrWhiteSpace(r.TaxId)).WithMessage("Invalid NIF.");
        RuleForEach(r => r.Specialties).Must(s => PainterRules.Specialties.Contains(s)).WithMessage("Unknown specialty.");
        RuleFor(r => r.Iban).Must(Iban.IsValidPortuguese).When(r => !string.IsNullOrWhiteSpace(r.Iban)).WithMessage("Invalid Portuguese IBAN.");
        RuleFor(r => r.Notes).MaximumLength(2000);
        RuleFor(r => r.Locale).IsInEnum();
    }
}

internal sealed class UpdatePainterRequestValidator : AbstractValidator<UpdatePainterRequest>
{
    public UpdatePainterRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Phone).Matches(PainterRules.PhonePattern).When(r => !string.IsNullOrWhiteSpace(r.Phone));
        RuleFor(r => r.TaxId).Must(TaxId.IsValid).When(r => !string.IsNullOrWhiteSpace(r.TaxId)).WithMessage("Invalid NIF.");
        RuleForEach(r => r.Specialties).Must(s => PainterRules.Specialties.Contains(s)).WithMessage("Unknown specialty.");
        RuleFor(r => r.Iban).Must(Iban.IsValidPortuguese).When(r => !string.IsNullOrWhiteSpace(r.Iban)).WithMessage("Invalid Portuguese IBAN.");
        RuleFor(r => r.Notes).MaximumLength(2000);
        RuleFor(r => r.Rating).InclusiveBetween(1, 5).Must(r => r is null || r * 10 == Math.Truncate(r.Value * 10)).WithMessage("One decimal at most.");
    }
}

internal sealed class AddDocumentRequestValidator : AbstractValidator<AddDocumentRequest>
{
    public AddDocumentRequestValidator()
    {
        RuleFor(r => r.FileId).NotEmpty();
        RuleFor(r => r.Kind).IsInEnum();
    }
}

internal sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(r => r.Phone).NotEmpty().Matches(PainterRules.PhonePattern);
        RuleFor(r => r.TaxId).Must(TaxId.IsValid).When(r => !string.IsNullOrWhiteSpace(r.TaxId)).WithMessage("Invalid NIF.");
        RuleFor(r => r.Municipalities).NotEmpty();
        RuleFor(r => r.Iban).Must(Iban.IsValidPortuguese).When(r => !string.IsNullOrWhiteSpace(r.Iban)).WithMessage("Invalid Portuguese IBAN.");
    }
}

/// <summary>Admin painters (A4.1–A4.4) and the painter's own profile.</summary>
internal static class PainterEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/painters").WithTags("Admin painters")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
        admin.MapGet("/", ListAsync).WithName("ListPainters");
        admin.MapPost("/", CreateAsync).WithValidation<CreatePainterRequest>().WithName("CreatePainter").ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapGet("/{id:guid}", DetailAsync).WithName("GetPainter").ProducesProblem(StatusCodes.Status404NotFound);
        admin.MapPut("/{id:guid}", UpdateAsync).WithValidation<UpdatePainterRequest>().WithName("UpdatePainter")
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapPost("/{id:guid}/invite", InviteAsync).WithName("InvitePainter").ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapPost("/{id:guid}/activate", (Guid id, PaintersDbContext db, IPainterAccounts accounts, CancellationToken ct) => SetActiveAsync(id, true, db, accounts, ct))
            .WithName("ActivatePainter");
        admin.MapPost("/{id:guid}/deactivate", (Guid id, PaintersDbContext db, IPainterAccounts accounts, CancellationToken ct) => SetActiveAsync(id, false, db, accounts, ct))
            .WithName("DeactivatePainter");
        admin.MapPost("/{id:guid}/documents/upload-url", DocumentUploadAsync).WithName("CreatePainterDocumentUpload")
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge).ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        admin.MapPost("/{id:guid}/documents", AddDocumentAsync).WithValidation<AddDocumentRequest>().WithName("AddPainterDocument");
        admin.MapDelete("/{id:guid}/documents/{documentId:guid}", DeleteDocumentAsync).WithName("DeletePainterDocument");

        var painter = app.MapGroup("/api/painter").WithTags("Painter")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Painter));
        painter.MapGet("/me", MeAsync).WithName("GetPainterProfile");
        painter.MapPut("/me", UpdateMeAsync).WithValidation<UpdateProfileRequest>().WithName("UpdatePainterProfile");
    }

    private static async Task<Ok<IReadOnlyList<PainterListItem>>> ListAsync(PaintersDbContext db, IPainterWork work, CancellationToken cancellationToken)
    {
        var items = new List<PainterListItem>();
        foreach (var p in await db.Painters.AsNoTracking().OrderBy(p => p.Name).ToListAsync(cancellationToken))
        {
            var done = (await work.ForPainterAsync(p.Id, cancellationToken)).Count(j => j.Status is JobStatus.Finished or JobStatus.Closed);
            items.Add(new PainterListItem(p.Id, p.Name, p.Email, p.Phone, p.Status, p.Municipalities, p.Specialties, p.Rating, done, p.CreatedAt));
        }

        return TypedResults.Ok<IReadOnlyList<PainterListItem>>(items);
    }

    private static async Task<Results<Created<PainterDetail>, ProblemHttpResult>> CreateAsync(
        CreatePainterRequest request, HttpContext http, PaintersDbContext db, IPainterAccounts accounts, ICoverageService coverage, IbanProtector protector,
        IEmailQueue emails, IMediaService media, IPainterWork work, TimeProvider clock, CancellationToken cancellationToken)
    {
        var municipalities = (request.Municipalities ?? []).Distinct().ToList();
        if (await UnknownMunicipalitiesAsync(municipalities, coverage, cancellationToken) is { } problem)
        {
            return problem;
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var locale = request.Locale ?? Locale.Pt;
        var userId = await accounts.CreateAsync(request.Name.Trim(), email, Blank(request.Phone), locale, cancellationToken);
        if (userId is null)
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.EmailTaken);
        }

        var painter = new Painter
        {
            UserId = userId.Value,
            Name = request.Name.Trim(),
            Email = email,
            Phone = Blank(request.Phone),
            TaxId = Blank(request.TaxId),
            Specialties = [.. (request.Specialties ?? []).Distinct()],
            Municipalities = municipalities,
            Notes = Blank(request.Notes),
            Status = PainterStatus.Invited,
        };
        SetIban(painter, request.Iban, protector);
        painter.Stamp(clock.GetUtcNow(), http.User.GetUserId());
        db.Painters.Add(painter);
        await db.SaveChangesAsync(cancellationToken);

        await emails.EnqueueAsync(new PainterInvitationEmail(email, locale, painter.Name, await accounts.InvitationLinkAsync(userId.Value, cancellationToken)), cancellationToken);
        return TypedResults.Created($"/api/admin/painters/{painter.Id}", await ToDetailAsync(painter, accounts, media, work, clock, cancellationToken));
    }

    private static async Task<Results<Ok<PainterDetail>, ProblemHttpResult>> DetailAsync(
        Guid id, PaintersDbContext db, IPainterAccounts accounts, IMediaService media, IPainterWork work, TimeProvider clock, CancellationToken cancellationToken)
    {
        var painter = await db.Painters.AsNoTracking().Include(p => p.Documents).SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        return painter is null ? NotFound() : TypedResults.Ok(await ToDetailAsync(painter, accounts, media, work, clock, cancellationToken));
    }

    private static async Task<Results<Ok<PainterDetail>, ProblemHttpResult>> UpdateAsync(
        Guid id, UpdatePainterRequest request, HttpContext http, PaintersDbContext db, IPainterAccounts accounts, ICoverageService coverage,
        IbanProtector protector, IMediaService media, IPainterWork work, TimeProvider clock, CancellationToken cancellationToken)
    {
        var painter = await db.Painters.Include(p => p.Documents).SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (painter is null)
        {
            return NotFound();
        }

        var municipalities = request.Municipalities.Distinct().ToList();
        if (await UnknownMunicipalitiesAsync(municipalities, coverage, cancellationToken) is { } problem)
        {
            return problem;
        }

        painter.Name = request.Name.Trim();
        painter.Phone = Blank(request.Phone);
        painter.TaxId = Blank(request.TaxId);
        painter.Specialties = [.. request.Specialties.Distinct()];
        painter.Municipalities = municipalities;
        painter.Notes = Blank(request.Notes);
        painter.Rating = request.Rating;
        if (request.Iban is not null)
        {
            SetIban(painter, request.Iban, protector);
        }

        painter.Stamp(clock.GetUtcNow(), http.User.GetUserId());
        if (!await db.TrySaveVersionAsync(painter, request.Version, cancellationToken))
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict);
        }

        await accounts.UpdateContactAsync(painter.UserId, painter.Name, painter.Phone, cancellationToken);
        return TypedResults.Ok(await ToDetailAsync(painter, accounts, media, work, clock, cancellationToken));
    }

    /// <summary>Sends the invitation again (A4.2) while the painter has not signed up.</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> InviteAsync(
        Guid id, PaintersDbContext db, IPainterAccounts accounts, IUserDirectory users, IEmailQueue emails, CancellationToken cancellationToken)
    {
        var painter = await db.Painters.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (painter is null)
        {
            return NotFound();
        }

        if (painter.Status == PainterStatus.Inactive || await accounts.HasSignedUpAsync(painter.UserId, cancellationToken))
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.InvalidTransition);
        }

        var locale = (await users.FindAsync(painter.UserId, cancellationToken))?.Locale ?? Locale.Pt;
        await emails.EnqueueAsync(new PainterInvitationEmail(painter.Email, locale, painter.Name, await accounts.InvitationLinkAsync(painter.UserId, cancellationToken)), cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>A deactivated painter gets no offers and cannot sign in; reactivated, they are active again or still invited.</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> SetActiveAsync(
        Guid id, bool active, PaintersDbContext db, IPainterAccounts accounts, CancellationToken cancellationToken)
    {
        var painter = await db.Painters.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (painter is null)
        {
            return NotFound();
        }

        painter.Status = !active ? PainterStatus.Inactive
            : await accounts.HasSignedUpAsync(painter.UserId, cancellationToken) ? PainterStatus.Active : PainterStatus.Invited;
        await db.SaveChangesAsync(cancellationToken);
        await accounts.SetActiveAsync(painter.UserId, active, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<SignedUpload>, ProblemHttpResult>> DocumentUploadAsync(
        Guid id, DocumentUploadRequest request, HttpContext http, PaintersDbContext db, IMediaService media, CancellationToken cancellationToken)
    {
        if (!await db.Painters.AnyAsync(p => p.Id == id, cancellationToken))
        {
            return NotFound();
        }

        var (upload, refusal) = await media.CreateOwnedUploadAsync(
            new OwnedUploadRequest(MediaContext.PainterDocument, http.User.GetUserId()!.Value, null, id, request.ContentType, request.SizeBytes), cancellationToken);
        return refusal switch
        {
            UploadRefusal.UnsupportedType => ApiProblem.Create(StatusCodes.Status415UnsupportedMediaType, ErrorCodes.UnsupportedMediaType),
            UploadRefusal.TooLarge => ApiProblem.Create(StatusCodes.Status413PayloadTooLarge, ErrorCodes.FileTooLarge),
            _ => TypedResults.Ok(upload!),
        };
    }

    private static async Task<Results<Ok<PainterDocumentDto>, ProblemHttpResult>> AddDocumentAsync(
        Guid id, AddDocumentRequest request, HttpContext http, PaintersDbContext db, IMediaService media, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (!await db.Painters.AnyAsync(p => p.Id == id, cancellationToken))
        {
            return NotFound();
        }

        var file = (await media.ConfirmOwnedAsync([request.FileId], http.User.GetUserId()!.Value, null, id, MediaContext.PainterDocument, cancellationToken))
            .SingleOrDefault();
        if (file is null)
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["fileId"] = ["The file has not been uploaded."] });
        }

        var document = new PainterDocument { PainterId = id, FileId = file.Id, Kind = request.Kind, ExpiresOn = request.ExpiresOn, CreatedAt = clock.GetUtcNow() };
        db.Documents.Add(document);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToDto(document, file, BusinessDays.Today(clock)));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteDocumentAsync(
        Guid id, Guid documentId, PaintersDbContext db, IMediaService media, CancellationToken cancellationToken)
    {
        var document = await db.Documents.SingleOrDefaultAsync(d => d.Id == documentId && d.PainterId == id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
        await media.DeleteAsync(document.FileId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<PainterProfile>, ProblemHttpResult>> MeAsync(
        HttpContext http, PaintersDbContext db, ICoverageService coverage, CancellationToken cancellationToken)
    {
        var painter = await db.Painters.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == http.User.GetUserId(), cancellationToken);
        return painter is null ? NotFound() : TypedResults.Ok(await ToProfileAsync(painter, coverage, cancellationToken));
    }

    private static async Task<Results<Ok<PainterProfile>, ProblemHttpResult>> UpdateMeAsync(
        UpdateProfileRequest request, HttpContext http, PaintersDbContext db, IPainterAccounts accounts, ICoverageService coverage,
        IbanProtector protector, TimeProvider clock, CancellationToken cancellationToken)
    {
        var userId = http.User.GetUserId()!.Value;
        var painter = await db.Painters.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (painter is null)
        {
            return NotFound();
        }

        var municipalities = request.Municipalities.Distinct().ToList();
        if (await UnknownMunicipalitiesAsync(municipalities, coverage, cancellationToken) is { } problem)
        {
            return problem;
        }

        var ibanChanges = !string.IsNullOrWhiteSpace(request.Iban);
        if (ibanChanges && painter.IbanEncrypted is not null
            && (request.CurrentPassword is null || !await accounts.CheckPasswordAsync(userId, request.CurrentPassword, cancellationToken)))
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["currentPassword"] = ["Enter your password to change the IBAN."] });
        }

        painter.Phone = Blank(request.Phone);
        painter.TaxId = Blank(request.TaxId);
        painter.Municipalities = municipalities;
        if (ibanChanges)
        {
            SetIban(painter, request.Iban, protector);
        }

        painter.Stamp(clock.GetUtcNow(), userId);
        await db.SaveChangesAsync(cancellationToken);
        await accounts.UpdateContactAsync(userId, painter.Name, painter.Phone, cancellationToken);
        return TypedResults.Ok(await ToProfileAsync(painter, coverage, cancellationToken));
    }

    private static void SetIban(Painter painter, string? iban, IbanProtector protector)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            painter.IbanEncrypted = null;
            painter.IbanLast4 = null;
            return;
        }

        var normalized = Iban.Normalize(iban);
        painter.IbanEncrypted = protector.Protect(normalized);
        painter.IbanLast4 = normalized[^4..];
    }

    private static async Task<ProblemHttpResult?> UnknownMunicipalitiesAsync(
        IReadOnlyList<string> municipalities, ICoverageService coverage, CancellationToken cancellationToken)
    {
        var known = await coverage.ListMunicipalitiesAsync(cancellationToken);
        var unknown = municipalities.Where(m => !known.Contains(m)).ToList();
        return unknown.Count == 0
            ? null
            : ApiProblem.Validation(new Dictionary<string, string[]> { ["municipalities"] = [$"Not in the service area: {string.Join(", ", unknown)}."] });
    }

    private static async Task<PainterDetail> ToDetailAsync(
        Painter p, IPainterAccounts accounts, IMediaService media, IPainterWork work, TimeProvider clock, CancellationToken cancellationToken)
    {
        var files = (await media.FindAsync([.. p.Documents.Select(d => d.FileId)], cancellationToken)).ToDictionary(f => f.Id);
        var today = BusinessDays.Today(clock);
        return new PainterDetail(
            p.Id, p.UserId, p.Name, p.Email, p.Phone, p.TaxId, p.Specialties, p.Municipalities,
            p.IbanLast4 is null ? null : Iban.Mask(p.IbanLast4), p.Notes, p.Rating, p.Status, await accounts.HasSignedUpAsync(p.UserId, cancellationToken),
            [.. p.Documents.OrderBy(d => d.CreatedAt).Select(d => ToDto(d, files.GetValueOrDefault(d.FileId), today))],
            await work.ForPainterAsync(p.Id, cancellationToken), p.Version, p.CreatedAt);
    }

    private static PainterDocumentDto ToDto(PainterDocument d, StoredFile? file, DateOnly today) =>
        new(d.Id, d.Kind, d.ExpiresOn, d.ExpiresOn < today, file);

    private static async Task<PainterProfile> ToProfileAsync(Painter p, ICoverageService coverage, CancellationToken cancellationToken) =>
        new(p.Name, p.Email, p.Phone, p.TaxId, p.Specialties, p.Municipalities, p.IbanLast4 is null ? null : Iban.Mask(p.IbanLast4), p.Status,
            await coverage.ListMunicipalitiesAsync(cancellationToken));

    private static ProblemHttpResult NotFound() => ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
