using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Pinta.Modules.Identity.Domain;
using Pinta.Modules.Identity.Features.ConfirmEmail;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Identity.Features.ResendConfirmation;

/// <summary>POST /api/auth/resend-confirmation — one per minute per user (C4.5).</summary>
internal static class ResendConfirmationEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/resend-confirmation", HandleAsync)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.ConfirmationResend)
            .WithName("ResendConfirmation")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        ClaimsPrincipal principal,
        UserManager<User> users,
        ConfirmationEmailSender confirmation,
        CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null)
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthenticated);
        }

        if (!user.EmailConfirmed)
        {
            await confirmation.SendAsync(user, cancellationToken);
        }

        return TypedResults.NoContent();
    }
}
