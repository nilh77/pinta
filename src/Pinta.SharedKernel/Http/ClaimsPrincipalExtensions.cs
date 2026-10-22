using System.Security.Claims;

namespace Pinta.SharedKernel.Http;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in user's id, or <c>null</c> for anonymous requests.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
