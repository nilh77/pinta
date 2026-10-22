using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Jobs.Data;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Jobs;

public static class JobsModule
{
    public static IHostApplicationBuilder AddJobsModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<JobsDbContext>(JobsDbContext.Schema);
        return builder;
    }

    public static IEndpointRouteBuilder MapJobsEndpoints(this IEndpointRouteBuilder app) => app;
}
