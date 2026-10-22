using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Pinta.Modules.Identity.Features.Antiforgery;

public sealed record AntiforgeryTokenResponse(string Token);

/// <summary>
/// GET /api/auth/antiforgery — token for the <c>X-XSRF-TOKEN</c> header (S1). The token is tied to the
/// signed-in user, so clients fetch a new one after login and logout.
/// </summary>
internal static class AntiforgeryEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/antiforgery", Handle).WithName("GetAntiforgeryToken");

    private static Ok<AntiforgeryTokenResponse> Handle(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return TypedResults.Ok(new AntiforgeryTokenResponse(tokens.RequestToken!));
    }
}
