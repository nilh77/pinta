using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Pinta.Modules.Identity.Contracts;

namespace Pinta.Api.IntegrationTests;

/// <summary>Jobs sent through the API by a new client, for the tests that start after submission.</summary>
public static class TestJobs
{
    private static object Room(string type, string paint, decimal? length = null, decimal? width = null, string? size = null,
        bool[]? walls = null, bool ceiling = true, bool damage = false, string? clientKey = null) => new
    {
        kind = "room", roomTypeId = type, length, width, size, walls = walls ?? [true, true, true, true], ceiling, paintId = paint, damage, clientKey,
    };

    /// <summary>TC01: bedroom 4 × 3, empty.</summary>
    public static readonly object[] Bedroom = [Room("bedroom", "matte", 4m, 3m, clientKey: "room-1")];

    /// <summary>TC09 of docs/pricing-engine.md (furnished).</summary>
    public static readonly object[] Flat =
    [
        Room("living", "matte", 5m, 4m, clientKey: "room-1"),
        Room("kitchen", "satin", 3.5m, 3m),
        Room("bedroom", "matte", 4m, 3.5m),
        Room("bedroom", "matte", size: "M", walls: [true, true, true, false]),
        Room("bathroom", "anti_damp", 2.5m, 2m, damage: true),
        Room("hallway", "matte", 6m, 1.2m, ceiling: false),
    ];

    public sealed record SentJob(HttpClient Client, string Email, Guid JobId, string Code);

    /// <summary>Registers a client (email confirmed when <paramref name="confirmEmail"/>) and sends a request.</summary>
    public static async Task<SentJob> SendAsync(
        PintaApiFactory factory, object[]? elements = null, string occupancy = "empty", Guid? draftId = null, bool confirmEmail = true,
        string locale = "pt")
    {
        var email = TestData.UniqueEmail();
        var client = await ApiClient.CreateAsync(factory);
        (await client.RegisterAsync(email, locale: locale, sendConfirmationEmail: false)).EnsureSuccessStatusCode();
        if (confirmEmail)
        {
            await ConfirmEmailAsync(factory, client);
        }

        var body = new
        {
            draftId = draftId ?? Guid.NewGuid(),
            occupancy,
            elements = elements ?? Bedroom,
            description = "Paredes brancas.",
            address = new { line1 = "Rua das Flores, 24", floor = "2.º Esq.", postalCode = "2710-123", locality = "Sintra", accessNotes = "Elevador" },
            preferredDates = new { asap = true },
            billing = (object?)null,
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/jobs") { Content = JsonContent.Create(body, options: ApiClient.Json) };
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await client.SendAsync(message);
        response.EnsureSuccessStatusCode();
        var job = await response.ReadJsonAsync();
        return new SentJob(client, email, job.GetProperty("id").GetGuid(), job.GetProperty("code").GetString()!);
    }

    /// <summary>Follows a fresh confirmation link for the signed-in user, as the email would.</summary>
    public static async Task ConfirmEmailAsync(PintaApiFactory factory, HttpClient client)
    {
        var userId = (await (await client.GetAsync("/api/auth/me")).ReadJsonAsync()).GetProperty("id").GetGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var link = new Uri(await scope.ServiceProvider.GetRequiredService<IEmailConfirmationLinks>().CreateAsync(userId));
        var query = QueryHelpers.ParseQuery(link.Query);
        var confirmed = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new { userId = query["userId"].ToString(), token = query["token"].ToString() }, ApiClient.Json);
        confirmed.EnsureSuccessStatusCode();
    }
}
