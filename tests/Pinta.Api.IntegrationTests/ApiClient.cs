using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pinta.Api.IntegrationTests;

/// <summary>A browser-like client: keeps cookies and sends <c>X-XSRF-TOKEN</c> like the web app does.</summary>
public static class ApiClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<HttpClient> CreateAsync(WebApplicationFactory<Program> factory, string? acceptLanguage = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        if (acceptLanguage is not null)
        {
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(acceptLanguage);
        }

        await client.RefreshAntiforgeryAsync();
        return client;
    }

    /// <summary>The token is bound to the signed-in user, so it is fetched again after login and logout.</summary>
    public static async Task RefreshAntiforgeryAsync(this HttpClient client)
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/auth/antiforgery", Json);
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", response.GetProperty("token").GetString());
    }

    /// <summary>A client signed in as the demo admin (docs/plan.md, "Datos de demo").</summary>
    public static async Task<HttpClient> CreateAdminAsync(PintaApiFactory factory, string? acceptLanguage = null)
    {
        var client = await CreateAsync(factory, acceptLanguage);
        var login = await client.LoginAsync(TestData.AdminEmail, TestData.DemoPassword);
        login.EnsureSuccessStatusCode();
        return client;
    }

    public static async Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password }, Json);
        await client.RefreshAntiforgeryAsync();
        return response;
    }

    public static async Task<HttpResponseMessage> RegisterAsync(
        this HttpClient client, string email, string password = TestData.Password, string locale = "pt", bool sendConfirmationEmail = true)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Ana Costa",
            email,
            phone = "+351912345678",
            password,
            acceptTerms = true,
            marketingConsent = false,
            locale,
            sendConfirmationEmail,
        }, Json);
        await client.RefreshAntiforgeryAsync();
        return response;
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Json);
}

public static class TestData
{
    public const string Password = "very-long-password";
    public const string DemoPassword = "Pinta-Demo-2026";
    public const string AdminEmail = "nathan@demo.pinta.pt";

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.test";
}
