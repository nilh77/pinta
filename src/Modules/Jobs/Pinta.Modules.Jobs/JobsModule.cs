using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Features.Get;
using Pinta.Modules.Jobs.Features.Review;
using Pinta.Modules.Jobs.Features.Submit;
using Pinta.SharedKernel.Data;
using Pinta.SharedKernel.Events;

namespace Pinta.Modules.Jobs;

public static class JobsModule
{
    public static IHostApplicationBuilder AddJobsModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<JobsDbContext>(JobsDbContext.Schema);
        builder.Services.AddValidatorsFromAssemblyContaining<SubmitJobRequest>(includeInternalTypes: true);
        builder.Services.AddEventHandler<EmailConfirmed, MoveToReviewWhenEmailConfirmed>();
        return builder;
    }

    public static IEndpointRouteBuilder MapJobsEndpoints(this IEndpointRouteBuilder app)
    {
        var jobs = app.MapGroup("/api/jobs").WithTags("Jobs");
        SubmitJobEndpoint.Map(jobs);
        GetJobEndpoint.Map(jobs);
        return app;
    }
}
