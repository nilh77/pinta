using Microsoft.Extensions.Configuration;
using Stripe;

namespace Pinta.Modules.Payments.Stripe;

/// <summary>Stripe test keys from user-secrets (docs/integrations.md); never in the repository.</summary>
public sealed class StripeOptions
{
    public string? SecretKey { get; init; }

    public string? PublishableKey { get; init; }

    public string? WebhookSecret { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(PublishableKey);

    public static StripeOptions FromConfiguration(IConfiguration configuration) => new()
    {
        SecretKey = configuration["Stripe:SecretKey"],
        PublishableKey = configuration["Stripe:PublishableKey"],
        WebhookSecret = configuration["Stripe:WebhookSecret"],
    };
}

/// <summary>A PaymentIntent as Pinta needs it.</summary>
public sealed record StripeIntent(string Id, string ClientSecret, string Status, long AmountCents);

/// <summary>The calls Pinta makes to Stripe; tests replace it with a fake.</summary>
public interface IStripeGateway
{
    bool IsConfigured { get; }

    string? PublishableKey { get; }

    /// <param name="methodTypes">Stripe payment method types (<c>card</c>, <c>mb_way</c>, <c>multibanco</c>).</param>
    Task<StripeIntent> CreatePaymentIntentAsync(
        long amountCents, IReadOnlyList<string> methodTypes, IReadOnlyDictionary<string, string> metadata, string idempotencyKey,
        CancellationToken cancellationToken);

    Task<StripeIntent> GetPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken);

    Task CancelPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken);

    /// <summary>The method the client actually paid with (<c>card</c>, <c>mb_way</c>, <c>multibanco</c>), or null.</summary>
    Task<string?> GetPaidMethodTypeAsync(string paymentIntentId, CancellationToken cancellationToken);

    /// <summary>Requests a refund; returns Stripe's refund id. The webhook <c>charge.refunded</c> completes it.</summary>
    Task<string> RefundAsync(
        string paymentIntentId, long amountCents, IReadOnlyDictionary<string, string> metadata, string idempotencyKey,
        CancellationToken cancellationToken);
}

internal sealed class StripeGateway(StripeOptions options) : IStripeGateway
{
    private readonly StripeClient? _client = options.IsConfigured ? new StripeClient(options.SecretKey) : null;

    public bool IsConfigured => _client is not null;

    public string? PublishableKey => options.PublishableKey;

    private StripeClient Client => _client ?? throw new InvalidOperationException("Stripe is not configured.");

    public async Task<StripeIntent> CreatePaymentIntentAsync(
        long amountCents, IReadOnlyList<string> methodTypes, IReadOnlyDictionary<string, string> metadata, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var intent = await new PaymentIntentService(Client).CreateAsync(
            new PaymentIntentCreateOptions
            {
                Amount = amountCents,
                Currency = "eur",
                AllowedPaymentMethodTypes = [.. methodTypes],
                Metadata = new Dictionary<string, string>(metadata),
            },
            new RequestOptions { IdempotencyKey = idempotencyKey },
            cancellationToken);
        return ToIntent(intent);
    }

    public async Task<StripeIntent> GetPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken) =>
        ToIntent(await new PaymentIntentService(Client).GetAsync(paymentIntentId, cancellationToken: cancellationToken));

    public Task CancelPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken) =>
        new PaymentIntentService(Client).CancelAsync(paymentIntentId, cancellationToken: cancellationToken);

    public async Task<string?> GetPaidMethodTypeAsync(string paymentIntentId, CancellationToken cancellationToken)
    {
        var intent = await new PaymentIntentService(Client).GetAsync(
            paymentIntentId, new PaymentIntentGetOptions { Expand = ["payment_method"] }, cancellationToken: cancellationToken);
        return intent.PaymentMethod?.Type;
    }

    public async Task<string> RefundAsync(
        string paymentIntentId, long amountCents, IReadOnlyDictionary<string, string> metadata, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var refund = await new RefundService(Client).CreateAsync(
            new RefundCreateOptions { PaymentIntent = paymentIntentId, Amount = amountCents, Metadata = new Dictionary<string, string>(metadata) },
            new RequestOptions { IdempotencyKey = idempotencyKey },
            cancellationToken);
        return refund.Id;
    }

    private static StripeIntent ToIntent(PaymentIntent intent) => new(intent.Id, intent.ClientSecret, intent.Status, intent.Amount);
}
