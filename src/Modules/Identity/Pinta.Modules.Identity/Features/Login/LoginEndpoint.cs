using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Pinta.Modules.Identity.Domain;
using Pinta.Modules.Identity.Features.Me;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Identity.Features.Login;

public sealed record LoginRequest(string Email, string Password);

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty();
        RuleFor(r => r.Password).NotEmpty();
    }
}

/// <summary>POST /api/auth/login — email and password; 5 failures lock the account for 15 minutes (S1).</summary>
internal static class LoginEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/login", HandleAsync)
            .WithValidation<LoginRequest>()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithName("Login")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<Results<Ok<MeResponse>, ProblemHttpResult>> HandleAsync(
        LoginRequest request, UserManager<User> users, SignInManager<User> signIn)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return InvalidCredentials();
        }

        // Locked-out and wrong-password look the same to the caller (generic message).
        var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return InvalidCredentials();
        }

        return TypedResults.Ok(await MeResponse.ForAsync(user, users));
    }

    private static ProblemHttpResult InvalidCredentials() =>
        ApiProblem.Create(StatusCodes.Status401Unauthorized, ErrorCodes.InvalidCredentials);
}
