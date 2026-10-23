using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Payments.Contracts;
using Pinta.Modules.Jobs.Features.Access;
using Pinta.Modules.Jobs.Features.Admin;
using Pinta.Modules.Jobs.Features.Assignments;
using Pinta.Modules.Jobs.Features.Client;
using Pinta.Modules.Jobs.Features.Expiry;
using Pinta.Modules.Jobs.Features.Payments;
using Pinta.Modules.Jobs.Features.Review;
using Pinta.Modules.Jobs.Features.Shared;
using Pinta.Modules.Jobs.Features.Submit;
using Pinta.SharedKernel.Data;
using Pinta.SharedKernel.Events;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Jobs;

public static class JobsModule
{
    public static IHostApplicationBuilder AddJobsModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<JobsDbContext>(JobsDbContext.Schema);
        builder.Services.AddValidatorsFromAssemblyContaining<SubmitJobRequest>(includeInternalTypes: true);
        builder.Services.AddEventHandler<EmailConfirmed, MoveToReviewWhenEmailConfirmed>();
        builder.Services.AddScoped<IJobAccess, JobAccess>();
        builder.Services.AddScoped<IPayableJobs, PayableJobs>();
        builder.Services.AddScoped<JobCancellation>();
        builder.Services.AddScoped<OfferReminders>();
        builder.Services.AddScoped<IPainterWork, PainterWork>();
        builder.Services.AddScoped<IFinanceJobs, Features.Finance.FinanceJobs>();
        builder.Services.AddEventHandler<PaymentSucceeded, MoveJobWhenPaid>();
        builder.Services.TryAddSingleton<WebLinks>();
        builder.Services.AddScoped<ExpireQuotes>();
        return builder;
    }

    public static IEndpointRouteBuilder MapJobsEndpoints(this IEndpointRouteBuilder app)
    {
        var jobs = app.MapGroup("/api/jobs").WithTags("Jobs");
        SubmitJobEndpoint.Map(jobs);
        ClientJobEndpoints.Map(jobs);

        var admin = app.MapGroup("/api/admin").WithTags("Admin jobs")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
        AdminJobQueries.Map(admin);
        AdminJobCommands.Map(admin);
        AssignmentEndpoints.Map(app);
        PainterJobEndpoints.Map(app);
        PainterVisitEndpoints.Map(app);
        return app;
    }
}
