using FluentValidation;
using Pinta.SharedKernel;

namespace Pinta.Modules.Identity.Features.Register;

/// <summary>
/// <see cref="SendConfirmationEmail"/> is false in the quote flow, where the request email carries the confirmation
/// link (one email instead of two, docs/integrations.md).
/// </summary>
public sealed record RegisterRequest(
    string Name,
    string Email,
    string Phone,
    string Password,
    bool AcceptTerms,
    bool MarketingConsent,
    Locale Locale,
    bool SendConfirmationEmail = true);

internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public const int MinPasswordLength = 10;

    public RegisterRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.Phone).NotEmpty().Matches(@"^\+[1-9]\d{7,14}$");
        RuleFor(r => r.Password).NotEmpty().MinimumLength(MinPasswordLength).MaximumLength(128);
        RuleFor(r => r.AcceptTerms).Equal(true);
        RuleFor(r => r.Locale).IsInEnum();
    }
}
