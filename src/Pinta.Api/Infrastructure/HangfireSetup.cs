using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Pinta.Modules.Identity.Contracts;

namespace Pinta.Api.Infrastructure;

/// <summary>Hangfire on PostgreSQL (schema <c>hangfire</c>); the dashboard at /hangfire is admin-only.</summary>
public static class HangfireSetup
{
    public static IHostApplicationBuilder AddHangfireJobs(this IHostApplicationBuilder builder)
    {
        // The build-time OpenAPI generator starts the app without a database; Hangfire is not part of the API.
        if (OpenApiGeneration.IsRunning)
        {
            return builder;
        }

        builder.Services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("pinta")),
                new PostgreSqlStorageOptions { SchemaName = "hangfire" }));
        builder.Services.AddHangfireServer();
        return builder;
    }

    public static IEndpointRouteBuilder MapHangfireAdminDashboard(this IEndpointRouteBuilder app)
    {
        if (OpenApiGeneration.IsRunning)
        {
            return app;
        }

        app.MapHangfireDashboard("/hangfire", new DashboardOptions
        {
            DashboardTitle = "Pinta · Hangfire",
            AppPath = "/",
            Authorization = [new AdminOnlyFilter()],
        });
        return app;
    }

    /// <summary>Hangfire answers 401 when signed out and 403 when signed in without the admin role.</summary>
    private sealed class AdminOnlyFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context) => context.GetHttpContext().User.IsInRole(RoleNames.Admin);
    }
}
