using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Painters.Contracts;
using Pinta.Modules.Painters.Data;
using Pinta.Modules.Painters.Features;
using Pinta.SharedKernel.Data;
using Pinta.SharedKernel.Events;

namespace Pinta.Modules.Painters;

public static class PaintersModule
{
    public static IHostApplicationBuilder AddPaintersModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<PaintersDbContext>(PaintersDbContext.Schema);
        builder.Services.AddSingleton<IbanProtector>();
        builder.Services.AddScoped<IPainterDirectory, PainterDirectory>();
        builder.Services.AddEventHandler<PainterSignedUp, ActivateOnSignUp>();
        builder.Services.AddValidatorsFromAssemblyContaining<CreatePainterRequest>(includeInternalTypes: true);
        return builder;
    }

    public static IEndpointRouteBuilder MapPaintersEndpoints(this IEndpointRouteBuilder app)
    {
        PainterEndpoints.Map(app);
        return app;
    }
}
