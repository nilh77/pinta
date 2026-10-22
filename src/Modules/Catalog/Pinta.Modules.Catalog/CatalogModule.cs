using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Catalog.Data;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Catalog;

public static class CatalogModule
{
    public static IHostApplicationBuilder AddCatalogModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<CatalogDbContext>(CatalogDbContext.Schema);
        return builder;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app) => app;
}
