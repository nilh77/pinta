using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Identity.Domain;
using Pinta.Modules.Identity.Features.ConfirmEmail;
using Pinta.Modules.Identity.Features.Me;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Identity.Features.Register;

/// <summary>POST /api/auth/register — creates a client account, signs it in and emails the confirmation link (C4.3).</summary>
internal static class RegisterEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/register", HandleAsync)
            .WithValidation<RegisterRequest>()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithName("Register")
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<Created<MeResponse>, ProblemHttpResult>> HandleAsync(
        RegisterRequest request,
        UserManager<User> users,
        SignInManager<User> signIn,
        ConfirmationEmailSender confirmation,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null)
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.EmailTaken);
        }

        var now = clock.GetUtcNow();
        var user = new User
        {
            UserName = email,
            Email = email,
            PhoneNumber = request.Phone,
            Name = request.Name.Trim(),
            Locale = request.Locale,
            MarketingConsent = request.MarketingConsent,
            TermsAcceptedAt = now,
            CreatedAt = now,
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            // A concurrent registration with the same email loses here.
            if (created.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.EmailTaken);
            }

            return ApiProblem.Validation(new Dictionary<string, string[]>
            {
                ["password"] = created.Errors.Select(e => e.Description).ToArray(),
            });
        }

        await users.AddToRoleAsync(user, RoleNames.Client);
        if (request.SendConfirmationEmail)
        {
            await confirmation.SendAsync(user, cancellationToken);
        }
        await signIn.SignInAsync(user, isPersistent: true);

        return TypedResults.Created("/api/auth/me", await MeResponse.ForAsync(user, users));
    }
}
