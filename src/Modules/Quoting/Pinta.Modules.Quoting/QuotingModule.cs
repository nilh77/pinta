using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Quoting.Data;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Quoting;

public static class QuotingModule
{
    public static IHostApplicationBuilder AddQuotingModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<QuotingDbContext>(QuotingDbContext.Schema);
        return builder;
    }

    public static IEndpointRouteBuilder MapQuotingEndpoints(this IEndpointRouteBuilder app) => app;
}
