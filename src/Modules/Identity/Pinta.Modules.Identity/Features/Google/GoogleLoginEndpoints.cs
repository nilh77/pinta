using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Identity.Domain;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Identity.Features.Google;

public sealed record AuthProvidersResponse(bool Google);

/// <summary>
/// Sign in with Google (C4.3), only when <c>Authentication:Google:ClientId</c> and <c>ClientSecret</c> are in
/// user-secrets. A new Google user is a client with a confirmed email; the web app returns to <c>returnUrl</c>.
/// </summary>
internal static class GoogleLoginEndpoints
{
    public const string Scheme = "Google";
    public const string CallbackPath = "/api/auth/google/signin";

    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/providers", (IConfiguration configuration) => TypedResults.Ok(new AuthProvidersResponse(IsConfigured(configuration))))
            .WithName("GetAuthProviders");
        group.MapGet("/google", Challenge).WithName("GoogleLogin").ProducesProblem(StatusCodes.Status404NotFound);
        group.MapGet("/google/callback", CallbackAsync).WithName("GoogleCallback");
    }

    private static IResult Challenge(string? returnUrl, IConfiguration configuration, SignInManager<User> signIn)
    {
        if (!IsConfigured(configuration))
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        var callback = $"/api/auth/google/callback?returnUrl={Uri.EscapeDataString(SafeReturnUrl(returnUrl))}";
        var properties = signIn.ConfigureExternalAuthenticationProperties(Scheme, callback);
        return TypedResults.Challenge(properties, [Scheme]);
    }

    private static async Task<RedirectHttpResult> CallbackAsync(
        string? returnUrl, SignInManager<User> signIn, UserManager<User> users, IConfiguration configuration, TimeProvider clock)
    {
        var webUrl = (configuration["Web:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var target = SafeReturnUrl(returnUrl);
        var info = await signIn.GetExternalLoginInfoAsync();
        var email = info?.Principal.FindFirstValue(ClaimTypes.Email);
        if (info is null || string.IsNullOrEmpty(email))
        {
            return TypedResults.Redirect($"{webUrl}{target}{(target.Contains('?') ? '&' : '?')}login=failed");
        }

        var user = await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey) ?? await users.FindByEmailAsync(email);
        if (user is null)
        {
            var now = clock.GetUtcNow();
            user = new User
            {
                UserName = email,
                Email = email,
                // Google has verified the address.
                EmailConfirmed = true,
                Name = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email,
                Locale = LocaleOf(target),
                CreatedAt = now,
                // The button says that continuing accepts the terms and privacy policy.
                TermsAcceptedAt = now,
            };
            await users.CreateAsync(user);
            await users.AddToRoleAsync(user, RoleNames.Client);
        }

        if (await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey) is null)
        {
            await users.AddLoginAsync(user, info);
        }

        await signIn.SignInAsync(user, isPersistent: true);
        return TypedResults.Redirect($"{webUrl}{target}");
    }

    /// <summary>Only paths inside the web app (S1): anything else goes to the home page.</summary>
    private static string SafeReturnUrl(string? returnUrl) =>
        returnUrl is { Length: > 0 } && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\")
            ? returnUrl
            : "/";

    private static Locale LocaleOf(string path) => path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() switch
    {
        "es" => Locale.Es,
        "en" => Locale.En,
        _ => Locale.Pt,
    };
}
