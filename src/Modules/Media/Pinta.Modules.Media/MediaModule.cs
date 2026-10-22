using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Media.Contracts;
using Pinta.Modules.Media.Data;
using Pinta.Modules.Media.Features.Uploads;
using Pinta.Modules.Media.Storage;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Media;

public static class MediaModule
{
    public static IHostApplicationBuilder AddMediaModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<MediaDbContext>(MediaDbContext.Schema);
        // Resolved on first use, so the build-time OpenAPI generation needs no storage.
        builder.Services.AddSingleton(sp => new ObjectStorage(StorageOptions.FromConfiguration(builder.Configuration), sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddScoped<IMediaService, MediaService>();
        builder.Services.AddScoped<MediaCleanup>();
        builder.Services.AddValidatorsFromAssemblyContaining<UploadUrlRequestValidator>(includeInternalTypes: true);
        return builder;
    }

    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        UploadEndpoints.Map(app);
        return app;
    }
}
