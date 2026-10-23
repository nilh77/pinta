using Hangfire;
using Pinta.Modules.Jobs.Features.Assignments;
using Pinta.Modules.Jobs.Features.Expiry;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Media.Features.Uploads;
using Pinta.Modules.Painters.Features;
using Pinta.SharedKernel.Time;

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

    /// <summary>Recurring jobs of the modules (docs/integrations.md, scheduled tasks).</summary>
    public static void ScheduleRecurringJobs(this IServiceProvider services)
    {
        if (OpenApiGeneration.IsRunning)
        {
            return;
        }

        var jobs = services.GetRequiredService<IRecurringJobManager>();
        var lisbon = new RecurringJobOptions { TimeZone = BusinessDays.Lisbon };
        jobs.AddOrUpdate<MediaCleanup>(MediaCleanup.JobId, cleanup => cleanup.RunAsync(), Cron.Daily(3), lisbon);
        jobs.AddOrUpdate<ExpireQuotes>(ExpireQuotes.JobId, expiry => expiry.RunAsync(), Cron.Daily(3), lisbon);
        jobs.AddOrUpdate<OfferReminders>(OfferReminders.JobId, reminders => reminders.RunAsync(), Cron.Hourly(), lisbon);
        jobs.AddOrUpdate<QuoteExpiryReminders>(QuoteExpiryReminders.JobId, reminders => reminders.RunAsync(), Cron.Daily(9), lisbon);
        jobs.AddOrUpdate<OverdueReviewAlerts>(OverdueReviewAlerts.JobId, alerts => alerts.RunAsync(), Cron.Daily(9), lisbon);
        jobs.AddOrUpdate<PainterDocumentAlerts>(PainterDocumentAlerts.JobId, alerts => alerts.RunAsync(), Cron.Weekly(DayOfWeek.Monday, 9), lisbon);
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
