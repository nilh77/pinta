using Microsoft.AspNetCore.Antiforgery;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Api.Infrastructure;

public static class AntiforgerySetup
{
    public const string HeaderName = "X-XSRF-TOKEN";

    public static IHostApplicationBuilder AddAntiforgeryHeader(this IHostApplicationBuilder builder)
    {
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = HeaderName;
            options.Cookie.Name = "pinta.antiforgery";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });
        return builder;
    }
}

/// <summary>
/// Requires a valid <c>X-XSRF-TOKEN</c> on every mutating <c>/api</c> request (JSON endpoints are not covered by the
/// built-in antiforgery middleware). Stripe webhooks are exempt: they are verified by signature.
/// </summary>
public sealed class AntiforgeryValidationMiddleware(RequestDelegate next, IAntiforgery antiforgery, IProblemDetailsService problems)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresToken(context.Request) && !await antiforgery.IsRequestValidAsync(context))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await problems.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails =
                {
                    Status = StatusCodes.Status403Forbidden,
                    Extensions = { [ApiProblem.CodeKey] = ErrorCodes.AntiforgeryFailed },
                },
            });
            return;
        }

        await next(context);
    }

    private static bool RequiresToken(HttpRequest request) =>
        !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) ||
          HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method))
        && request.Path.StartsWithSegments("/api")
        && !request.Path.StartsWithSegments("/api/webhooks");
}
