using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Identity.Features.Painters;
using Pinta.SharedKernel;

namespace Pinta.Api.IntegrationTests;

public static class TestPainters
{
    /// <summary>A painter account created like the admin does, and its invitation link's query.</summary>
    public static async Task<(Guid UserId, string Email, Dictionary<string, string> Link)> InviteAsync(PintaApiFactory factory)
    {
        var email = $"painter-{Guid.NewGuid():N}@example.test";
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IPainterAccounts>();
        var userId = (await accounts.CreateAsync("Rui Almeida", email, "+351911111111", Locale.Pt))!.Value;
        var link = new Uri(await accounts.InvitationLinkAsync(userId));
        Assert.Equal("/pt/pintor/alta", link.AbsolutePath);
        return (userId, email, QueryHelpers.ParseQuery(link.Query).ToDictionary(q => q.Key, q => q.Value.ToString()));
    }
}

/// <summary>A4.2: painters sign up with the admin's invitation (72 h, once) and can be deactivated.</summary>
public class PainterInvitationTests(PintaApiFactory factory)
{
    [Fact]
    public async Task A4_2_the_painter_sets_a_password_with_the_invitation_and_is_signed_in_as_painter()
    {
        var (_, email, link) = await TestPainters.InviteAsync(factory);
        var client = await ApiClient.CreateAsync(factory);

        var accepted = await client.PostAsJsonAsync("/api/auth/accept-invite", new { userId = link["userId"], token = link["token"], password = "painter-password-1" }, ApiClient.Json);
        await client.RefreshAntiforgeryAsync();
        var again = await client.PostAsJsonAsync("/api/auth/accept-invite", new { userId = link["userId"], token = link["token"], password = "other-password-12" }, ApiClient.Json);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("painter", (await accepted.ReadJsonAsync()).GetProperty("role").GetString());
        await ProblemAssert.HasCodeAsync(again, HttpStatusCode.UnprocessableEntity, "validation_failed");
        var fresh = await ApiClient.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await fresh.LoginAsync(email, "painter-password-1")).StatusCode);
    }

    [Fact]
    public async Task A4_2_a_deactivated_painter_cannot_sign_up_or_sign_in_until_reactivated()
    {
        var (userId, email, link) = await TestPainters.InviteAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPainterAccounts>().SetActiveAsync(userId, false);
        }

        var client = await ApiClient.CreateAsync(factory);
        var refused = await client.PostAsJsonAsync("/api/auth/accept-invite", new { userId = link["userId"], token = link["token"], password = "painter-password-1" }, ApiClient.Json);
        await ProblemAssert.HasCodeAsync(refused, HttpStatusCode.UnprocessableEntity, "validation_failed");

        // Deactivating also voids the link sent before; after reactivating, the admin sends a new one.
        string token;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IPainterAccounts>();
            await accounts.SetActiveAsync(userId, true);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/api/auth/accept-invite",
                new { userId = link["userId"], token = link["token"], password = "painter-password-1" }, ApiClient.Json)).StatusCode);
            token = QueryHelpers.ParseQuery(new Uri(await accounts.InvitationLinkAsync(userId)).Query)["token"].ToString();
        }

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/accept-invite",
            new { userId = link["userId"], token, password = "painter-password-1" }, ApiClient.Json)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPainterAccounts>().SetActiveAsync(userId, false);
        }

        var other = await ApiClient.CreateAsync(factory);
        await ProblemAssert.HasCodeAsync(await other.LoginAsync(email, "painter-password-1"), HttpStatusCode.Unauthorized, "invalid_credentials");
    }

    [Fact]
    public async Task A4_2_a_client_account_cannot_use_accept_invite()
    {
        var email = TestData.UniqueEmail();
        var client = await ApiClient.CreateAsync(factory);
        (await client.RegisterAsync(email)).EnsureSuccessStatusCode();
        var userId = (await (await client.GetAsync("/api/auth/me")).ReadJsonAsync()).GetProperty("id").GetGuid();

        var response = await client.PostAsJsonAsync("/api/auth/accept-invite", new { userId, token = "x", password = "painter-password-1" }, ApiClient.Json);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
    }

    [Fact]
    public void A4_2_invitations_last_72_hours() =>
        Assert.Equal(TimeSpan.FromHours(72), factory.Services.GetRequiredService<IOptions<PainterInvitationTokenProviderOptions>>().Value.TokenLifespan);
}
