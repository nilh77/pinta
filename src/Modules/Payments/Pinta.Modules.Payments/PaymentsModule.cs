using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Payments.Data;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Payments;

public static class PaymentsModule
{
    public static IHostApplicationBuilder AddPaymentsModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<PaymentsDbContext>(PaymentsDbContext.Schema);
        return builder;
    }

    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder app) => app;
}
