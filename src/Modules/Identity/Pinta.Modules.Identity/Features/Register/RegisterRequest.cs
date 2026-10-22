using FluentValidation;
using Pinta.SharedKernel;

namespace Pinta.Modules.Identity.Features.Register;

public sealed record RegisterRequest(
    string Name,
    string Email,
    string Phone,
    string Password,
    bool AcceptTerms,
    bool MarketingConsent,
    Locale Locale);

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
