namespace Pinta.SharedKernel;

/// <summary>Stable error codes returned in ProblemDetails <c>code</c> (docs/api.md). The frontend translates by code.</summary>
public static class ErrorCodes
{
    public const string BadRequest = "bad_request";
    public const string IdempotencyKeyRequired = "idempotency_key_required";
    public const string Unauthenticated = "unauthenticated";
    public const string InvalidCredentials = "invalid_credentials";
    public const string Forbidden = "forbidden";
    public const string EmailNotConfirmed = "email_not_confirmed";
    public const string AntiforgeryFailed = "antiforgery_failed";
    public const string NotFound = "not_found";
    public const string InvalidTransition = "invalid_transition";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string IdempotencyConflict = "idempotency_conflict";
    public const string EmailTaken = "email_taken";
    public const string AssignmentTaken = "assignment_taken";
    public const string QuoteExpired = "quote_expired";
    public const string FileTooLarge = "file_too_large";
    public const string UnsupportedMediaType = "unsupported_media_type";
    public const string ValidationFailed = "validation_failed";
    public const string MediaLimitReached = "media_limit_reached";
    public const string OutOfCoverage = "out_of_coverage";
    public const string OverMaxAmount = "over_max_amount";
    public const string PaymentMethodNotAllowed = "payment_method_not_allowed";
    public const string PaymentAlreadyMade = "payment_already_made";
    public const string RefundNotAllowed = "refund_not_allowed";
    public const string PeriodClosed = "period_closed";
    public const string RateLimited = "rate_limited";
    public const string PaymentProviderError = "payment_provider_error";
    public const string InternalError = "internal_error";

    /// <summary>Code used when a response only has a status (e.g. a bare 401 from the auth middleware).</summary>
    public static string ForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => BadRequest,
        StatusCodes.Status401Unauthorized => Unauthenticated,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status422UnprocessableEntity => ValidationFailed,
        StatusCodes.Status429TooManyRequests => RateLimited,
        _ => InternalError,
    };
}
