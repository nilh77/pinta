using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Finance.Data;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Finance;

public static class FinanceModule
{
    public static IHostApplicationBuilder AddFinanceModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<FinanceDbContext>(FinanceDbContext.Schema);
        return builder;
    }

    public static IEndpointRouteBuilder MapFinanceEndpoints(this IEndpointRouteBuilder app) => app;
}
