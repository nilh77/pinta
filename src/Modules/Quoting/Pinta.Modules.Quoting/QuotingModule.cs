using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Quoting.Data;
using Pinta.Modules.Quoting.Features.Estimate;
using Pinta.Modules.Quoting.Features.Pricing;
using Pinta.Modules.Quoting.Features.Simulate;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Quoting;

public static class QuotingModule
{
    public static IHostApplicationBuilder AddQuotingModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<QuotingDbContext>(QuotingDbContext.Schema);
        builder.Services.AddScoped<QuotePricing>();
        builder.Services.AddValidatorsFromAssemblyContaining<EstimateRequest>(includeInternalTypes: true);
        return builder;
    }

    public static IEndpointRouteBuilder MapQuotingEndpoints(this IEndpointRouteBuilder app)
    {
        EstimateEndpoint.Map(app);

        var admin = app.MapGroup("/api/admin").WithTags("Admin quotes")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
        SimulateEndpoint.Map(admin);
        return app;
    }
}
