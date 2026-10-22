using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Pinta.SharedKernel.Data;
using Pinta.SharedKernel.Http;

namespace Pinta.SharedKernel.Idempotency;

/// <summary>
/// Requires <c>Idempotency-Key</c> (a UUID) on a signed-in POST and replays its first successful response (C4.4: a
/// retry after a network failure never creates a second job). A placeholder row is written before the handler runs,
/// so two concurrent requests with the same key cannot both execute.
/// </summary>
public sealed class IdempotencyFilter<TRequest>(IdempotencyDbContext db, TimeProvider clock, IOptions<JsonOptions> json) : IEndpointFilter
{
    public const string HeaderName = "Idempotency-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (!Guid.TryParse(http.Request.Headers[HeaderName], out var key))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, ErrorCodes.IdempotencyKeyRequired);
        }

        var userId = http.User.GetUserId() ?? Guid.Empty;
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, json.Value.SerializerOptions)));
        var cancellationToken = http.RequestAborted;

        var existing = await db.Records.AsNoTracking().SingleOrDefaultAsync(r => r.UserId == userId && r.Key == key, cancellationToken);
        if (existing is not null)
        {
            return Replay(existing, hash);
        }

        var record = new IdempotencyRecord { UserId = userId, Key = key, RequestHash = hash, CreatedAt = clock.GetUtcNow() };
        db.Records.Add(record);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another request with this key got in first.
            db.ChangeTracker.Clear();
            existing = await db.Records.AsNoTracking().SingleAsync(r => r.UserId == userId && r.Key == key, cancellationToken);
            return Replay(existing, hash);
        }

        object? result;
        try
        {
            result = await next(context);
        }
        catch
        {
            await ForgetAsync(record);
            throw;
        }

        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK;
        if (status is < 200 or > 299)
        {
            // Failures are not remembered: the client may fix the request and try again with the same key.
            await ForgetAsync(record);
            return result;
        }

        record.StatusCode = status;
        record.ResponseBody = result is IValueHttpResult { Value: { } value } ? JsonSerializer.Serialize(value, value.GetType(), json.Value.SerializerOptions) : null;
        record.Location = result switch
        {
            IResult r when r.GetType().GetProperty("Location")?.GetValue(r) is string location => location,
            _ => null,
        };
        await db.SaveChangesAsync(CancellationToken.None);
        return result;
    }

    private static IResult Replay(IdempotencyRecord record, string hash)
    {
        if (record.RequestHash != hash || record.StatusCode == 0)
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.IdempotencyConflict);
        }

        return new StoredResult(record);
    }

    private async Task ForgetAsync(IdempotencyRecord record)
    {
        db.Records.Remove(record);
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private sealed class StoredResult(IdempotencyRecord record) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = record.StatusCode;
            if (record.Location is not null)
            {
                httpContext.Response.Headers.Location = record.Location;
            }

            if (record.ResponseBody is not null)
            {
                httpContext.Response.ContentType = "application/json; charset=utf-8";
                await httpContext.Response.WriteAsync(record.ResponseBody, Encoding.UTF8);
            }
        }
    }
}

public static class IdempotencyExtensions
{
    public static RouteHandlerBuilder RequireIdempotencyKey<TRequest>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<IdempotencyFilter<TRequest>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

    public static IServiceCollection AddIdempotency(this IServiceCollection services)
    {
        services.AddModuleDbContext<IdempotencyDbContext>(IdempotencyDbContext.Schema);
        return services;
    }
}
