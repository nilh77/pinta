using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Painters.Data;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Painters;

public static class PaintersModule
{
    public static IHostApplicationBuilder AddPaintersModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<PaintersDbContext>(PaintersDbContext.Schema);
        return builder;
    }

    public static IEndpointRouteBuilder MapPaintersEndpoints(this IEndpointRouteBuilder app) => app;
}
