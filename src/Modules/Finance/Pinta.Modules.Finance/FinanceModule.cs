using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Finance.Data;
using Pinta.Modules.Finance.Features.Payouts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.SharedKernel.Data;
using Pinta.SharedKernel.Events;

namespace Pinta.Modules.Finance;

public static class FinanceModule
{
    public static IHostApplicationBuilder AddFinanceModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<FinanceDbContext>(FinanceDbContext.Schema);
        builder.Services.AddEventHandler<JobClosed, CreatePayoutOnClose>();
        return builder;
    }

    public static IEndpointRouteBuilder MapFinanceEndpoints(this IEndpointRouteBuilder app)
    {
        PayoutEndpoints.Map(app);
        return app;
    }
}
