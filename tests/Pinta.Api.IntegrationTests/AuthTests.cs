using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Pinta.Modules.Notifications.Contracts;
using Pinta.SharedKernel;

namespace Pinta.Api.IntegrationTests;

public class AuthTests(PintaApiFactory factory)
{
    [Fact]
    public async Task C4_3_C4_5_S1_register_confirm_email_then_sign_in()
    {
        var email = TestData.UniqueEmail();
        var client = await ApiClient.CreateAsync(factory);

        var registered = await client.RegisterAsync(email);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var me = await registered.ReadJsonAsync();
        Assert.Equal("client", me.GetProperty("role").GetString());
        Assert.Equal("pt", me.GetProperty("locale").GetString());
        Assert.False(me.GetProperty("emailConfirmed").GetBoolean());

        // Registration leaves the client signed in, still unconfirmed.
        var meBefore = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        Assert.False(meBefore.GetProperty("emailConfirmed").GetBoolean());

        var confirmation = Assert.Single(factory.Emails.To<ConfirmEmailEmail>(email));
        Assert.Equal(Locale.Pt, confirmation.Locale);
        var link = new Uri(confirmation.ConfirmationUrl);
        Assert.Equal("http://web.test/pt/confirmar-email", link.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(link.Query);

        var confirmed = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new { userId = query["userId"].ToString(), token = query["token"].ToString() }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        await client.RefreshAntiforgeryAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        var login = await client.LoginAsync(email, TestData.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var meAfter = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        Assert.True(meAfter.GetProperty("emailConfirmed").GetBoolean());
        Assert.Equal(email, meAfter.GetProperty("email").GetString());
    }

    [Fact]
    public async Task C4_3_confirmation_email_uses_the_chosen_language()
    {
        var email = TestData.UniqueEmail();
        var client = await ApiClient.CreateAsync(factory);

        await client.RegisterAsync(email, locale: "en");

        var confirmation = Assert.Single(factory.Emails.To<ConfirmEmailEmail>(email));
        Assert.Equal(Locale.En, confirmation.Locale);
        Assert.StartsWith("http://web.test/en/confirmar-email?", confirmation.ConfirmationUrl);
    }

    [Fact]
    public async Task C4_3_registering_a_taken_email_returns_409_email_taken()
    {
        var email = TestData.UniqueEmail();
        await (await ApiClient.CreateAsync(factory)).RegisterAsync(email);

        var response = await (await ApiClient.CreateAsync(factory)).RegisterAsync(email.ToUpperInvariant());

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Conflict, "email_taken");
    }

    [Fact]
    public async Task C4_3_invalid_registration_returns_422_with_errors_per_field()
    {
        var client = await ApiClient.CreateAsync(factory);

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "",
            email = "not-an-email",
            phone = "912345678",
            password = "short",
            acceptTerms = false,
            marketingConsent = false,
            locale = "pt",
        }, ApiClient.Json);

        var problem = await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
        var errors = problem.GetProperty("errors");
        foreach (var field in new[] { "name", "email", "phone", "password", "acceptTerms" })
        {
            Assert.True(errors.TryGetProperty(field, out _), $"Missing error for {field}.");
        }
    }

    [Fact]
    public async Task S1_wrong_password_returns_401_invalid_credentials()
    {
        var email = TestData.UniqueEmail();
        var client = await ApiClient.CreateAsync(factory);
        await client.RegisterAsync(email);

        var response = await (await ApiClient.CreateAsync(factory)).LoginAsync(email, "wrong-password-123");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Unauthorized, "invalid_credentials");
    }

    [Fact]
    public async Task S1_unknown_email_returns_the_same_401_invalid_credentials()
    {
        var response = await (await ApiClient.CreateAsync(factory)).LoginAsync(TestData.UniqueEmail(), TestData.Password);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Unauthorized, "invalid_credentials");
    }

    [Fact]
    public async Task S1_five_failed_attempts_lock_the_account()
    {
        var email = TestData.UniqueEmail();
        await (await ApiClient.CreateAsync(factory)).RegisterAsync(email);
        var client = await ApiClient.CreateAsync(factory);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await client.LoginAsync(email, "wrong-password-123");
        }

        var response = await client.LoginAsync(email, TestData.Password);
        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Unauthorized, "invalid_credentials");
    }

    [Fact]
    public async Task C4_5_invalid_confirmation_token_returns_422()
    {
        var email = TestData.UniqueEmail();
        var client = await ApiClient.CreateAsync(factory);
        var me = await (await client.RegisterAsync(email)).ReadJsonAsync();

        var response = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new { userId = me.GetProperty("id").GetString(), token = "bm90LWEtdG9rZW4" }, ApiClient.Json);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
    }

    [Fact]
    public async Task C4_5_resend_confirmation_is_limited_to_one_per_minute()
    {
        var email = TestData.UniqueEmail();
        var client = await ApiClient.CreateAsync(factory);
        await client.RegisterAsync(email);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/resend-confirmation", null)).StatusCode);
        var second = await client.PostAsync("/api/auth/resend-confirmation", null);

        await ProblemAssert.HasCodeAsync(second, HttpStatusCode.TooManyRequests, "rate_limited");
        Assert.True(second.Headers.Contains("Retry-After"));
        Assert.Equal(2, factory.Emails.To<ConfirmEmailEmail>(email).Count);
    }

    [Fact]
    public async Task Me_without_session_returns_401_unauthenticated()
    {
        var response = await (await ApiClient.CreateAsync(factory)).GetAsync("/api/auth/me");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Unauthorized, "unauthenticated");
    }

    [Fact]
    public async Task Seeded_admins_sign_in_with_the_demo_password()
    {
        foreach (var email in new[] { "nathan@demo.pinta.pt", "socio@demo.pinta.pt" })
        {
            var client = await ApiClient.CreateAsync(factory);
            var login = await client.LoginAsync(email, TestData.DemoPassword);

            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var me = await login.ReadJsonAsync();
            Assert.Equal("admin", me.GetProperty("role").GetString());
            Assert.True(me.GetProperty("emailConfirmed").GetBoolean());
        }
    }
}
