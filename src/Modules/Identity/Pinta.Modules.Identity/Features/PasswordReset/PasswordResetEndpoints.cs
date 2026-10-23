using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pinta.Modules.Identity.Domain;
using Pinta.Modules.Identity.Features.ConfirmEmail;
using Pinta.Modules.Identity.Features.Register;
using Pinta.Modules.Notifications.Contracts;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Identity.Features.PasswordReset;

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(Guid UserId, string Token, string Password);

internal sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator() => RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
}

internal sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(r => r.UserId).NotEmpty();
        RuleFor(r => r.Token).NotEmpty();
        RuleFor(r => r.Password).NotEmpty().MinimumLength(RegisterRequestValidator.MinPasswordLength).MaximumLength(128);
    }
}

/// <summary>Reset links live 2 hours (S1), apart from the 48 h of the email confirmation links.</summary>
internal sealed class PasswordResetTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PasswordResetTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<User>> logger)
    : DataProtectorTokenProvider<User>(dataProtectionProvider, options, logger)
{
    public const string ProviderName = "PasswordReset";
}

public sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public PasswordResetTokenProviderOptions()
    {
        Name = PasswordResetTokenProvider.ProviderName;
        TokenLifespan = TimeSpan.FromHours(2);
    }
}

/// <summary>POST /api/auth/forgot-password and /reset-password (S1).</summary>
internal static class PasswordResetEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/forgot-password", ForgotAsync)
            .WithValidation<ForgotPasswordRequest>()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithName("ForgotPassword")
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        group.MapPost("/reset-password", ResetAsync)
            .WithValidation<ResetPasswordRequest>()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithName("ResetPassword")
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    // Same answer whether the email exists or not, so the endpoint does not reveal accounts.
    private static async Task<NoContent> ForgotAsync(
        ForgotPasswordRequest request, UserManager<User> users, IEmailQueue emails, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is not null)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var webUrl = (configuration["Web:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
            var url = $"{webUrl}/{user.Locale.ToString().ToLowerInvariant()}/restablecer?userId={user.Id}&token={encodedToken}";
            await emails.EnqueueAsync(new ResetPasswordEmail(user.Email!, user.Locale, user.Name, url), cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ResetAsync(
        ResetPasswordRequest request, UserManager<User> users, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        var token = ConfirmationEmailSender.DecodeToken(request.Token);
        if (user is null || token is null)
        {
            return InvalidLink();
        }

        var result = await users.ResetPasswordAsync(user, token, request.Password);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? InvalidLink()
                : ApiProblem.Validation(new Dictionary<string, string[]> { ["password"] = [.. result.Errors.Select(e => e.Description)] });
        }

        // A new password opens an account locked by failed attempts.
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return TypedResults.NoContent();
    }

    private static ProblemHttpResult InvalidLink() =>
        ApiProblem.Validation(new Dictionary<string, string[]> { ["token"] = ["The reset link is invalid or has expired."] });
}
