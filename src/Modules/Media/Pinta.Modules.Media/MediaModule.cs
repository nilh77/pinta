using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Media.Data;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Media;

public static class MediaModule
{
    public static IHostApplicationBuilder AddMediaModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<MediaDbContext>(MediaDbContext.Schema);
        return builder;
    }

    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app) => app;
}
