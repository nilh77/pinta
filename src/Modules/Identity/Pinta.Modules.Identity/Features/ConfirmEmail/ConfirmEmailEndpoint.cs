using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Pinta.Modules.Identity.Domain;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Identity.Features.ConfirmEmail;

public sealed record ConfirmEmailRequest(Guid UserId, string Token);

internal sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(r => r.UserId).NotEmpty();
        RuleFor(r => r.Token).NotEmpty();
    }
}

/// <summary>POST /api/auth/confirm-email — confirms the email with the token from the link (C4.5).</summary>
internal static class ConfirmEmailEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/confirm-email", HandleAsync)
            .WithValidation<ConfirmEmailRequest>()
            .WithName("ConfirmEmail");

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        ConfirmEmailRequest request, UserManager<User> users)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is { EmailConfirmed: true })
        {
            return TypedResults.NoContent();
        }

        var token = ConfirmationEmailSender.DecodeToken(request.Token);
        // Unknown user, malformed or expired token: same answer, so the endpoint does not reveal accounts.
        if (user is null || token is null || !(await users.ConfirmEmailAsync(user, token)).Succeeded)
        {
            return ApiProblem.Validation(new Dictionary<string, string[]>
            {
                ["token"] = ["The confirmation link is invalid or has expired."],
            });
        }

        return TypedResults.NoContent();
    }
}
