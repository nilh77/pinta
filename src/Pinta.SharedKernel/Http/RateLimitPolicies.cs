namespace Pinta.SharedKernel.Http;

/// <summary>Rate limiter policy names; the API host defines the limits.</summary>
public static class RateLimitPolicies
{
    /// <summary>Login and registration: 10 per minute per IP.</summary>
    public const string Auth = "auth";

    /// <summary>Resend the confirmation email: 1 per minute per user.</summary>
    public const string ConfirmationResend = "confirmation-resend";

    /// <summary>Public forms without an account (waitlist, upload URLs): same limit as login, per IP.</summary>
    public const string PublicForm = "public-form";

    /// <summary>Public price estimate: 60 per minute per IP.</summary>
    public const string Estimate = "estimate";
}
