using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Pinta.Modules.Catalog.Contracts;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Domain;
using Pinta.Modules.Jobs.Resources;
using Pinta.Modules.Media.Contracts;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Quoting.Contracts;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;
using Pinta.SharedKernel.Idempotency;
using Pinta.SharedKernel.Time;

namespace Pinta.Modules.Jobs.Features.Submit;

public sealed record AddressRequest(string Line1, string? Floor, string PostalCode, string Locality, string? AccessNotes);

/// <summary>"As soon as possible", or a range of start dates (C4.2).</summary>
public sealed record PreferredDatesRequest(bool Asap, DateOnly? From, DateOnly? To);

/// <summary>Invoice to a company: NIF with check digit and company name (C4.3).</summary>
public sealed record BillingRequest(string? TaxId, string? CompanyName);

/// <summary>
/// The request built in the quote flow (docs/api.md, POST /api/jobs). <see cref="DraftId"/> is the draft's random id:
/// photos uploaded with it are attached to the job.
/// </summary>
public sealed record SubmitJobRequest(
    Guid DraftId,
    Occupancy Occupancy,
    IReadOnlyList<QuoteElementRequest> Elements,
    string? Description,
    AddressRequest Address,
    PreferredDatesRequest PreferredDates,
    BillingRequest? Billing);

public sealed record SubmitJobResponse(Guid Id, string Code, JobStatus Status);

internal sealed class SubmitJobRequestValidator : AbstractValidator<SubmitJobRequest>
{
    public const int MaxDescription = 2000;
    public const int MaxRangeDays = 60;

    public SubmitJobRequestValidator()
    {
        RuleFor(r => r.DraftId).NotEmpty();
        RuleFor(r => r.Occupancy).IsInEnum();
        RuleFor(r => r.Elements).NotNull();
        RuleFor(r => r.Description).MaximumLength(MaxDescription);
        RuleFor(r => r.Address).NotNull().ChildRules(address =>
        {
            address.RuleFor(a => a.Line1).NotEmpty().MaximumLength(200);
            address.RuleFor(a => a.Floor).MaximumLength(50);
            address.RuleFor(a => a.PostalCode).NotEmpty().Matches(PostalCode.Pattern).WithMessage(_ => JobsMessages.Get("postal_code_format"));
            address.RuleFor(a => a.Locality).NotEmpty().MaximumLength(100);
            address.RuleFor(a => a.AccessNotes).MaximumLength(500);
        });
        RuleFor(r => r.PreferredDates).NotNull().ChildRules(dates =>
        {
            dates.RuleFor(d => d).Must(d => d.Asap ? d.From is null && d.To is null : d.From is not null && d.To is not null)
                .OverridePropertyName("From").WithMessage(_ => JobsMessages.Get("dates_choose"));
            dates.RuleFor(d => d.To).Must((d, to) => to is null || d.From is null || to >= d.From).WithMessage(_ => JobsMessages.Get("dates_order"));
            dates.RuleFor(d => d.To).Must((d, to) => to is null || d.From is null || to.Value.DayNumber - d.From.Value.DayNumber <= MaxRangeDays)
                .WithMessage(_ => JobsMessages.Get("dates_too_long"));
        });
        When(r => r.Billing is not null && (!string.IsNullOrWhiteSpace(r.Billing.TaxId) || !string.IsNullOrWhiteSpace(r.Billing.CompanyName)), () =>
        {
            RuleFor(r => r.Billing!.TaxId).Must(TaxId.IsValid).WithMessage(_ => JobsMessages.Get("tax_id"));
            RuleFor(r => r.Billing!.CompanyName).NotEmpty().WithMessage(_ => JobsMessages.Get("company_required")).MaximumLength(200);
        });
    }
}

/// <summary>POST /api/jobs — the client sends the request (C4.4). Idempotent with <c>Idempotency-Key</c>.</summary>
internal static class SubmitJobEndpoint
{
    public static void Map(RouteGroupBuilder jobs) =>
        jobs.MapPost("/", HandleAsync)
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Client))
            .WithValidation<SubmitJobRequest>()
            .RequireIdempotencyKey<SubmitJobRequest>()
            .WithName("SubmitJob")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

    private static async Task<Results<Created<SubmitJobResponse>, ProblemHttpResult>> HandleAsync(
        SubmitJobRequest request,
        HttpContext http,
        JobsDbContext db,
        IUserDirectory userDirectory,
        IEmailConfirmationLinks confirmationLinks,
        ICoverageService coverage,
        IRequestSettings settingsProvider,
        IQuoteVersions quotes,
        IMediaService media,
        IEmailQueue emails,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var client = await userDirectory.FindAsync(http.User.GetUserId()!.Value, cancellationToken);
        if (client is null)
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthenticated);
        }

        var municipality = await coverage.FindMunicipalityAsync(request.Address.PostalCode, cancellationToken);
        if (municipality is null)
        {
            return TypedResults.Problem(new HttpValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["address.postalCode"] = [JobsMessages.Get("out_of_coverage")],
            })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Extensions = { [ApiProblem.CodeKey] = ErrorCodes.OutOfCoverage },
            });
        }

        var settings = await settingsProvider.GetAsync(cancellationToken);
        var today = BusinessDays.Today(clock);
        var earliest = BusinessDays.Add(today, settings.MinLeadBusinessDays);
        if (!request.PreferredDates.Asap && request.PreferredDates.From < earliest)
        {
            return ApiProblem.Validation(new Dictionary<string, string[]>
            {
                ["preferredDates.from"] = [JobsMessages.Get("dates_too_soon", earliest.ToString("d", System.Globalization.CultureInfo.CurrentUICulture))],
            });
        }

        var jobId = Guid.CreateVersion7();
        var (quote, errors) = await quotes.CreateEstimateAsync(jobId, client.Id, request.Occupancy, request.Elements, cancellationToken);
        if (errors is not null)
        {
            return ApiProblem.Validation(errors.Errors);
        }

        var now = clock.GetUtcNow();
        var hasInvoice = !string.IsNullOrWhiteSpace(request.Billing?.TaxId);
        var job = new Job
        {
            Id = jobId,
            Code = await db.NextCodeAsync(today.Year, cancellationToken),
            ClientId = client.Id,
            AddressLine1 = request.Address.Line1.Trim(),
            Floor = Blank(request.Address.Floor),
            PostalCode = request.Address.PostalCode,
            Locality = request.Address.Locality.Trim(),
            Municipality = municipality,
            AccessNotes = Blank(request.Address.AccessNotes),
            Description = Blank(request.Description),
            PreferredAsap = request.PreferredDates.Asap,
            PreferredFrom = request.PreferredDates.From,
            PreferredTo = request.PreferredDates.To,
            IsPremium = quote!.Premium,
            TaxId = hasInvoice ? request.Billing!.TaxId : null,
            CompanyName = hasInvoice ? request.Billing!.CompanyName!.Trim() : null,
            CurrentQuoteVersionId = quote.Id,
            CreatedAt = now,
        };
        job.Submit(client.EmailConfirmed, now, () => BusinessDays.Add(today, settings.ReviewDeadlineBusinessDays));
        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        await media.AttachDraftFilesAsync(request.DraftId, job.Id, client.Id, quote.ElementIdsByClientKey, cancellationToken);

        var confirmationUrl = client.EmailConfirmed ? null : await confirmationLinks.CreateAsync(client.Id, cancellationToken);
        await emails.EnqueueAsync(new RequestReceivedEmail(
            client.Email, client.Locale, client.Name, job.Code, quote.AreaM2, quote.RangeMinCents, quote.RangeMaxCents,
            settings.ReviewDeadlineBusinessDays, confirmationUrl), cancellationToken);

        return TypedResults.Created($"/api/jobs/{job.Id}", new SubmitJobResponse(job.Id, job.Code, job.Status));
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
