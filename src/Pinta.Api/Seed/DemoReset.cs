using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Media.Storage;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Api.Seed;

/// <summary>
/// «Restablecer datos de demo» (docs/plan.md, H8): only in Development. Empties every module's tables except the two
/// admins (who stay signed in) and Hangfire, removes the stored files and seeds the demo again with dates from today.
/// </summary>
public static class DemoReset
{
    // Every module schema; identity (the admins stay) and hangfire are left out.
    private const string TruncateModules = """
        DO $$
        DECLARE t record;
        BEGIN
          FOR t IN SELECT schemaname, tablename FROM pg_tables
                   WHERE schemaname IN ('catalog', 'quoting', 'jobs', 'painters', 'payments', 'media', 'finance', 'notifications')
                     AND tablename NOT LIKE '%migrations%'
          LOOP
            EXECUTE format('TRUNCATE TABLE %I.%I RESTART IDENTITY CASCADE', t.schemaname, t.tablename);
          END LOOP;
        END $$;
        """;
    private static readonly SemaphoreSlim Running = new(1, 1);

    /// <summary>Always mapped, so the OpenAPI client knows it; outside Development it answers 404 and does nothing.</summary>
    public static void Map(WebApplication app) =>
        app.MapPost("/api/admin/demo/reset", ResetAsync)
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin))
            .WithTags("Admin settings")
            .WithName("ResetDemoData")
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<NoContent, ProblemHttpResult>> ResetAsync(
        IServiceProvider services, IHostEnvironment environment, JobsDbContext db, ObjectStorage storage, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        // One reset at a time: a second press while the first runs waits for it.
        await Running.WaitAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlRawAsync(TruncateModules, cancellationToken);

            // Everyone but the partners; their roles and logins go with them.
            var admins = DemoSeeder.AdminEmails.Select(e => e.ToUpperInvariant()).ToArray();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM identity.users WHERE normalized_email IS NULL OR NOT (normalized_email = ANY({admins}))", cancellationToken);

            await storage.DeleteAllAsync(cancellationToken);
            await DemoSeeder.SeedAsync(services, adminsKept: true);
            return TypedResults.NoContent();
        }
        finally
        {
            Running.Release();
        }
    }
}
