using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pinta.Modules.Payments.Stripe;

namespace Pinta.Api.IntegrationTests;

/// <summary>Stripe for the tests: PaymentIntents and refunds in memory, and webhooks signed like Stripe signs them.</summary>
public sealed class FakeStripe : IStripeGateway
{
    public const string WebhookSecret = "whsec_test_pinta";

    private readonly ConcurrentDictionary<string, StripeIntent> _intents = new();

    /// <summary>The method each intent was paid with (default <c>card</c>).</summary>
    public ConcurrentDictionary<string, string> PaidMethods { get; } = new();

    public ConcurrentQueue<(string PaymentIntentId, long AmountCents, string IdempotencyKey)> Refunds { get; } = new();

    public ConcurrentQueue<string> Cancelled { get; } = new();

    public bool IsConfigured => true;

    public string PublishableKey => "pk_test_fake";

    public Task<StripeIntent> CreatePaymentIntentAsync(
        long amountCents, IReadOnlyList<string> methodTypes, IReadOnlyDictionary<string, string> metadata, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var id = $"pi_fake_{Guid.NewGuid():N}";
        var intent = new StripeIntent(id, $"{id}_secret_x", "requires_payment_method", amountCents);
        _intents[id] = intent;
        MethodTypes[id] = methodTypes;
        return Task.FromResult(intent);
    }

    public ConcurrentDictionary<string, IReadOnlyList<string>> MethodTypes { get; } = new();

    public StripeIntent Intent(string id) => _intents[id];

    public Task<StripeIntent> GetPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken) =>
        Task.FromResult(_intents[paymentIntentId]);

    public Task CancelPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken)
    {
        Cancelled.Enqueue(paymentIntentId);
        return Task.CompletedTask;
    }

    public Task<string?> GetPaidMethodTypeAsync(string paymentIntentId, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(PaidMethods.GetValueOrDefault(paymentIntentId, "card"));

    public Task<string> RefundAsync(
        string paymentIntentId, long amountCents, IReadOnlyDictionary<string, string> metadata, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Refunds.Enqueue((paymentIntentId, amountCents, idempotencyKey));
        return Task.FromResult($"re_fake_{idempotencyKey}");
    }

    /// <summary>Posts an event to the webhook with a valid <c>Stripe-Signature</c> (t=…,v1=HMAC-SHA256).</summary>
    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, string type, object dataObject, string? eventId = null, string secret = WebhookSecret)
    {
        var json = JsonSerializer.Serialize(new
        {
            id = eventId ?? $"evt_{Guid.NewGuid():N}",
            @object = "event",
            type,
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            livemode = false,
            data = new { @object = dataObject },
        });
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{json}")));
        using var content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe") { Content = content };
        message.Headers.Add("Stripe-Signature", $"t={timestamp},v1={signature}");
        return await client.SendAsync(message);
    }

    public static object Succeeded(string paymentIntentId, long amountCents) =>
        new { id = paymentIntentId, @object = "payment_intent", amount = amountCents, currency = "eur", status = "succeeded" };
}
