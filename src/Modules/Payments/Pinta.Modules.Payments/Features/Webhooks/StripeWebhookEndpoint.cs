using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pinta.Modules.Payments.Data;
using Pinta.Modules.Payments.Domain;
using Pinta.Modules.Payments.Stripe;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;
using Stripe;

namespace Pinta.Modules.Payments.Features.Webhooks;

/// <summary>
/// POST /api/webhooks/stripe (S4): the signature is checked against <c>Stripe:WebhookSecret</c> (400 if it fails) and
/// the event id is stored before anything else, so a repeated event answers 200 without repeating its effects. A
/// failure rolls everything back and answers 500, and Stripe retries.
/// </summary>
internal static class StripeWebhookEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/webhooks/stripe", HandleAsync)
            .WithTags("Payments")
            .AllowAnonymous()
            .ExcludeFromDescription();

    private static async Task<Results<Ok, ProblemHttpResult>> HandleAsync(
        HttpRequest request, StripeOptions options, PaymentsDbContext db, WebhookProcessor processor, TimeProvider clock,
        ILogger<WebhookProcessor> logger, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var json = await reader.ReadToEndAsync(cancellationToken);
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json, request.Headers["Stripe-Signature"], options.WebhookSecret ?? "", throwOnApiVersionMismatch: false);
        }
        catch (StripeException e)
        {
            logger.LogWarning("Rejected a Stripe webhook: {Reason}", e.Message);
            return ApiProblem.Create(StatusCodes.Status400BadRequest, ErrorCodes.BadRequest);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.StripeEvents.Add(new StripeEvent { Id = stripeEvent.Id, Type = stripeEvent.Type, Payload = json, ProcessedAt = clock.GetUtcNow() });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            logger.LogInformation("Stripe event {EventId} already processed", stripeEvent.Id);
            return TypedResults.Ok();
        }

        await processor.ProcessAsync(stripeEvent, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Ok();
    }
}
