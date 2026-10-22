using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Pinta.Modules.Identity.Domain;

namespace Pinta.Modules.Identity.Features.Logout;

/// <summary>POST /api/auth/logout.</summary>
internal static class LogoutEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/logout", HandleAsync)
            .RequireAuthorization()
            .WithName("Logout")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<NoContent> HandleAsync(SignInManager<User> signIn)
    {
        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }
}
