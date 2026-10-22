using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Api.Infrastructure;

/// <summary>Per-IP and per-user limits (NFR "Seguridad"); 429 <c>rate_limited</c> with <c>Retry-After</c>.</summary>
public static class RateLimitSetup
{
    public static IHostApplicationBuilder AddRateLimits(this IHostApplicationBuilder builder)
    {
        var authPerMinute = builder.Configuration.GetValue("RateLimits:AuthPerMinute", 10);
        var estimatePerMinute = builder.Configuration.GetValue("RateLimits:EstimatePerMinute", 60);

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => PerMinute(authPerMinute)));
            options.AddPolicy(RateLimitPolicies.PublicForm, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => PerMinute(authPerMinute)));
            options.AddPolicy(RateLimitPolicies.Uploads, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => PerMinute(builder.Configuration.GetValue("RateLimits:UploadsPerMinute", 120))));
            options.AddPolicy(RateLimitPolicies.Estimate, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => PerMinute(estimatePerMinute)));
            options.AddPolicy(RateLimitPolicies.ConfirmationResend, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
                _ => PerMinute(1)));
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(
                    new ProblemDetailsContext
                    {
                        HttpContext = context.HttpContext,
                        ProblemDetails =
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Extensions = { [ApiProblem.CodeKey] = ErrorCodes.RateLimited },
                        },
                    });
            };
        });
        return builder;
    }

    private static FixedWindowRateLimiterOptions PerMinute(int permits) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    };
}
