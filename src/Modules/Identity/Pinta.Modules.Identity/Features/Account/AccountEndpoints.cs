using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Identity.Domain;
using Pinta.Modules.Identity.Features.ConfirmEmail;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Notifications.Contracts;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Identity.Features.Account;

/// <summary>Screen 12 (C5.8): the client's own data; the invoice details are the defaults proposed in new requests.</summary>
public sealed record AccountProfile(
    string Name, string Email, bool EmailConfirmed, string? Phone, Locale Locale, string? TaxId, string? CompanyName, bool MarketingConsent,
    DateTimeOffset? DeletionRequestedAt);

public sealed record UpdateAccountRequest(
    string Name, string Email, string Phone, Locale Locale, string? TaxId, string? CompanyName, bool MarketingConsent);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>A client waiting for the deletion (C5.8) and what it would do to each of their jobs.</summary>
public sealed record DeletionRequest(Guid UserId, string Name, string Email, DateTimeOffset RequestedAt, IReadOnlyList<ClientJobForDeletion> Jobs);

internal sealed class UpdateAccountRequestValidator : AbstractValidator<UpdateAccountRequest>
{
    public UpdateAccountRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.Phone).NotEmpty().Matches(@"^\+[1-9]\d{7,14}$");
        RuleFor(r => r.Locale).IsInEnum();
        RuleFor(r => r.TaxId).Matches(@"^\d{9}$").When(r => !string.IsNullOrWhiteSpace(r.TaxId));
        RuleFor(r => r.CompanyName).MaximumLength(200);
    }
}

internal sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(r => r.CurrentPassword).NotEmpty();
        RuleFor(r => r.NewPassword).NotEmpty().MinimumLength(10).MaximumLength(128);
    }
}

/// <summary>GET/PUT /api/me, password, deletion request (C5.8) and the admins' deletion queue in Ajustes.</summary>
internal static class AccountEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/me").WithTags("Account").RequireAuthorization(policy => policy.RequireRole(RoleNames.Client));
        me.MapGet("", GetAsync).WithName("GetAccount");
        me.MapPut("", UpdateAsync).WithValidation<UpdateAccountRequest>().WithName("UpdateAccount").ProducesProblem(StatusCodes.Status409Conflict);
        me.MapPost("/password", ChangePasswordAsync).WithValidation<ChangePasswordRequest>().WithName("ChangePassword");
        me.MapPost("/delete-request", RequestDeletionAsync).WithName("RequestAccountDeletion");

        var admin = app.MapGroup("/api/admin/deletion-requests").WithTags("Admin settings")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
        admin.MapGet("", ListAsync).WithName("ListDeletionRequests");
        admin.MapPost("/{userId:guid}/execute", ExecuteAsync).WithName("ExecuteAccountDeletion")
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<Results<Ok<AccountProfile>, ProblemHttpResult>> GetAsync(HttpContext http, UserManager<User> users)
    {
        var user = await users.GetUserAsync(http.User);
        return user is null ? ApiProblem.Create(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthenticated) : TypedResults.Ok(ToProfile(user));
    }

    /// <summary>A new email has to be confirmed again; a taken one is <c>email_taken</c>.</summary>
    private static async Task<Results<Ok<AccountProfile>, ProblemHttpResult>> UpdateAsync(
        UpdateAccountRequest request, HttpContext http, UserManager<User> users, ConfirmationEmailSender confirmation, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(http.User);
        if (user is null)
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthenticated);
        }

        var email = request.Email.Trim();
        var emailChanged = !string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase);
        if (emailChanged && await users.FindByEmailAsync(email) is not null)
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.EmailTaken);
        }

        user.Name = request.Name.Trim();
        user.PhoneNumber = request.Phone.Trim();
        user.Locale = request.Locale;
        user.TaxId = string.IsNullOrWhiteSpace(request.TaxId) ? null : request.TaxId.Trim();
        user.CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? null : request.CompanyName.Trim();
        user.MarketingConsent = request.MarketingConsent;
        if (emailChanged)
        {
            user.Email = email;
            user.UserName = email;
            user.EmailConfirmed = false;
        }

        var saved = await users.UpdateAsync(user);
        if (!saved.Succeeded)
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["email"] = [.. saved.Errors.Select(e => e.Description)] });
        }

        if (emailChanged)
        {
            await confirmation.SendAsync(user, cancellationToken);
        }

        return TypedResults.Ok(ToProfile(user));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangePasswordAsync(
        ChangePasswordRequest request, HttpContext http, UserManager<User> users, SignInManager<User> signIn)
    {
        var user = await users.GetUserAsync(http.User);
        if (user is null)
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthenticated);
        }

        if (!await users.CheckPasswordAsync(user, request.CurrentPassword))
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["currentPassword"] = ["The current password is not correct."] });
        }

        var changed = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!changed.Succeeded)
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["newPassword"] = [.. changed.Errors.Select(e => e.Description)] });
        }

        // The security stamp changed: keep this session signed in, the others end.
        await signIn.RefreshSignInAsync(user);
        return TypedResults.NoContent();
    }

    /// <summary>C5.8: the request reaches the admins once; they execute it from Ajustes.</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> RequestDeletionAsync(
        HttpContext http, UserManager<User> users, IUserDirectory directory, IEmailQueue emails, WebLinks links, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(http.User);
        if (user is null)
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthenticated);
        }

        if (user.DeletionRequestedAt is null)
        {
            user.DeletionRequestedAt = clock.GetUtcNow();
            await users.UpdateAsync(user);
            foreach (var admin in await directory.ListAdminsAsync(cancellationToken))
            {
                await emails.EnqueueAsync(new AccountDeletionRequestedEmail(admin.Email, admin.Locale, user.Name, user.Email!, links.AdminSettings(admin.Locale)),
                    cancellationToken);
            }
        }

        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<DeletionRequest>>> ListAsync(
        UserManager<User> users, IClientAccountJobs jobs, CancellationToken cancellationToken)
    {
        var pending = await users.Users.AsNoTracking()
            .Where(u => u.DeletionRequestedAt != null && u.AnonymizedAt == null)
            .OrderBy(u => u.DeletionRequestedAt)
            .ToListAsync(cancellationToken);
        var result = new List<DeletionRequest>();
        foreach (var user in pending)
        {
            result.Add(new DeletionRequest(user.Id, user.Name, user.Email!, user.DeletionRequestedAt!.Value, await jobs.ForDeletionAsync(user.Id, cancellationToken)));
        }

        return TypedResults.Ok<IReadOnlyList<DeletionRequest>>(result);
    }

    /// <summary>
    /// C5.8: the client's jobs are cancelled with the policy and stripped of personal data (Jobs), then the account loses
    /// its name, email, phone, invoice details, password and logins and can never sign in again. Accounts stay.
    /// </summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
        Guid userId, HttpContext http, UserManager<User> users, IClientAccountJobs jobs, TimeProvider clock, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || user.DeletionRequestedAt is null || user.AnonymizedAt is not null)
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        // A job in progress or with its balance due blocks it: the work and the payment finish first.
        if (!await jobs.RemoveClientAsync(user.Id, http.User.GetUserId()!.Value, cancellationToken))
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.InvalidTransition);
        }

        foreach (var login in await users.GetLoginsAsync(user))
        {
            await users.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);
        }

        await users.RemovePasswordAsync(user);
        var anonymous = $"deleted-{user.Id:N}@anonymous.invalid";
        user.Name = "—";
        user.Email = anonymous;
        user.UserName = anonymous;
        user.EmailConfirmed = false;
        user.PhoneNumber = null;
        user.TaxId = null;
        user.CompanyName = null;
        user.MarketingConsent = false;
        user.IsDisabled = true;
        user.AnonymizedAt = clock.GetUtcNow();
        await users.UpdateAsync(user);
        await users.UpdateSecurityStampAsync(user);
        return TypedResults.NoContent();
    }

    private static AccountProfile ToProfile(User u) =>
        new(u.Name, u.Email!, u.EmailConfirmed, u.PhoneNumber, u.Locale, u.TaxId, u.CompanyName, u.MarketingConsent, u.DeletionRequestedAt);
}
