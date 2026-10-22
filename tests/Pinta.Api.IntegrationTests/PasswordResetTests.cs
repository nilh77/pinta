using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pinta.Modules.Identity.Features.PasswordReset;
using Pinta.Modules.Notifications.Contracts;

namespace Pinta.Api.IntegrationTests;

public class PasswordResetTests(PintaApiFactory factory)
{
    [Fact]
    public async Task S1_forgot_then_reset_password_signs_in_with_the_new_one()
    {
        var email = $"reset-{Guid.NewGuid():N}@test.pt";
        var client = await ApiClient.CreateAsync(factory);
        (await client.RegisterAsync(email, locale: "es")).EnsureSuccessStatusCode();
        var anonymous = await ApiClient.CreateAsync(factory);

        var forgot = await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { email }, ApiClient.Json);

        Assert.Equal(HttpStatusCode.NoContent, forgot.StatusCode);
        var resetEmail = Assert.Single(factory.Emails.To<ResetPasswordEmail>(email));
        var link = new Uri(resetEmail.ResetUrl);
        Assert.Equal("http://web.test/es/restablecer", link.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(link.Query);

        var reset = await anonymous.PostAsJsonAsync("/api/auth/reset-password",
            new { userId = query["userId"].ToString(), token = query["token"].ToString(), password = "new-password-123" }, ApiClient.Json);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.LoginAsync(email, TestData.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.LoginAsync(email, "new-password-123")).StatusCode);

        // The link works once.
        var again = await anonymous.PostAsJsonAsync("/api/auth/reset-password",
            new { userId = query["userId"].ToString(), token = query["token"].ToString(), password = "other-password-123" }, ApiClient.Json);
        var problem = await ProblemAssert.HasCodeAsync(again, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("token", out _));
    }

    [Fact]
    public async Task S1_unknown_email_gets_the_same_answer_and_no_email()
    {
        var email = $"nobody-{Guid.NewGuid():N}@test.pt";
        var client = await ApiClient.CreateAsync(factory);

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email }, ApiClient.Json);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(factory.Emails.To<ResetPasswordEmail>(email));
    }

    [Fact]
    public async Task S1_short_password_or_bad_token_returns_422()
    {
        var client = await ApiClient.CreateAsync(factory);

        var shortPassword = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { userId = Guid.NewGuid(), token = "x", password = "short" }, ApiClient.Json);
        var badToken = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { userId = Guid.NewGuid(), token = "not-a-token", password = "long-enough-password" }, ApiClient.Json);

        Assert.True((await ProblemAssert.HasCodeAsync(shortPassword, HttpStatusCode.UnprocessableEntity, "validation_failed")).GetProperty("errors").TryGetProperty("password", out _));
        Assert.True((await ProblemAssert.HasCodeAsync(badToken, HttpStatusCode.UnprocessableEntity, "validation_failed")).GetProperty("errors").TryGetProperty("token", out _));
    }

    [Fact]
    public void S1_reset_links_are_valid_two_hours()
    {
        var options = factory.Services.GetRequiredService<IOptions<PasswordResetTokenProviderOptions>>().Value;

        Assert.Equal(TimeSpan.FromHours(2), options.TokenLifespan);
    }
}
