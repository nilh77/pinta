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
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Identity.Domain;
using Pinta.Modules.Identity.Features.ConfirmEmail;
using Pinta.Modules.Identity.Features.Me;
using Pinta.Modules.Identity.Features.Register;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Identity.Features.Painters;

/// <summary>Invitation links last 72 h (A4.2), apart from the other token lifetimes.</summary>
internal sealed class PainterInvitationTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PainterInvitationTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<User>> logger)
    : DataProtectorTokenProvider<User>(dataProtectionProvider, options, logger)
{
    public const string ProviderName = "PainterInvitation";
    public const string Purpose = "painter-invitation";
}

public sealed class PainterInvitationTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public PainterInvitationTokenProviderOptions()
    {
        Name = PainterInvitationTokenProvider.ProviderName;
        TokenLifespan = TimeSpan.FromHours(72);
    }
}

internal sealed class PainterAccounts(UserManager<User> users, IConfiguration configuration, TimeProvider clock) : IPainterAccounts
{
    public async Task<Guid?> CreateAsync(string name, string email, string? phone, Locale locale, CancellationToken cancellationToken = default)
    {
        if (await users.FindByEmailAsync(email) is not null)
        {
            return null;
        }

        var user = new User { UserName = email, Email = email, PhoneNumber = phone, Name = name, Locale = locale, CreatedAt = clock.GetUtcNow() };
        var created = await users.CreateAsync(user);
        if (!created.Succeeded)
        {
            return null;
        }

        await users.AddToRoleAsync(user, RoleNames.Painter);
        return user.Id;
    }

    public async Task<string> InvitationLinkAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await Find(userId);
        var token = await users.GenerateUserTokenAsync(user, PainterInvitationTokenProvider.ProviderName, PainterInvitationTokenProvider.Purpose);
        var webUrl = (configuration["Web:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        return $"{webUrl}/{user.Locale.ToString().ToLowerInvariant()}/pintor/alta?userId={user.Id}&token={encoded}";
    }

    public async Task<bool> HasSignedUpAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await users.HasPasswordAsync(await Find(userId));

    public async Task SetActiveAsync(Guid userId, bool active, CancellationToken cancellationToken = default)
    {
        var user = await Find(userId);
        user.IsDisabled = !active;
        await users.UpdateAsync(user);
        if (!active)
        {
            // Ends existing sessions at the next security stamp check.
            await users.UpdateSecurityStampAsync(user);
        }
    }

    public async Task UpdateContactAsync(Guid userId, string name, string? phone, CancellationToken cancellationToken = default)
    {
        var user = await Find(userId);
        user.Name = name;
        user.PhoneNumber = phone;
        await users.UpdateAsync(user);
    }

    private async Task<User> Find(Guid userId) =>
        await users.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException($"User {userId} not found.");
}

public sealed record AcceptInviteRequest(Guid UserId, string Token, string Password);

internal sealed class AcceptInviteRequestValidator : AbstractValidator<AcceptInviteRequest>
{
    public AcceptInviteRequestValidator()
    {
        RuleFor(r => r.UserId).NotEmpty();
        RuleFor(r => r.Token).NotEmpty();
        RuleFor(r => r.Password).NotEmpty().MinimumLength(RegisterRequestValidator.MinPasswordLength).MaximumLength(128);
    }
}

/// <summary>
/// POST /api/auth/accept-invite (A4.2): the painter sets the password with the invitation (72 h, once), the email counts
/// as confirmed and the session starts.
/// </summary>
internal static class AcceptInviteEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/accept-invite", HandleAsync)
            .WithValidation<AcceptInviteRequest>()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithName("AcceptInvite")
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<Results<Ok<MeResponse>, ProblemHttpResult>> HandleAsync(
        AcceptInviteRequest request, UserManager<User> users, SignInManager<User> signIn)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        var token = ConfirmationEmailSender.DecodeToken(request.Token);
        // A used link (password already set), a wrong one, a disabled painter or another role: the same answer.
        if (user is null || token is null || user.IsDisabled || await users.HasPasswordAsync(user) || !await users.IsInRoleAsync(user, RoleNames.Painter)
            || !await users.VerifyUserTokenAsync(user, PainterInvitationTokenProvider.ProviderName, PainterInvitationTokenProvider.Purpose, token))
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["token"] = ["The invitation is invalid or has expired."] });
        }

        var added = await users.AddPasswordAsync(user, request.Password);
        if (!added.Succeeded)
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["password"] = [.. added.Errors.Select(e => e.Description)] });
        }

        user.EmailConfirmed = true;
        await users.UpdateAsync(user);
        await signIn.SignInAsync(user, isPersistent: true);
        return TypedResults.Ok(await MeResponse.ForAsync(user, users));
    }
}
