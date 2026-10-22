using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Pinta.Modules.Identity.Domain;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Identity.Features.Me;

/// <summary>GET /api/auth/me — current user, role and language (S1).</summary>
internal static class MeEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/me", HandleAsync)
            .RequireAuthorization()
            .WithName("GetMe")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<Results<Ok<MeResponse>, ProblemHttpResult>> HandleAsync(
        ClaimsPrincipal principal, UserManager<User> users)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null)
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthenticated);
        }

        return TypedResults.Ok(await MeResponse.ForAsync(user, users));
    }
}
