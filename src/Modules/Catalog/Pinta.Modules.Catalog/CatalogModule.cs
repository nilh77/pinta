using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Catalog.Contracts;
using Pinta.Modules.Catalog.Data;
using Pinta.Modules.Catalog.Features.Admin;
using Pinta.Modules.Catalog.Features.Coverage;
using Pinta.Modules.Catalog.Features.PublicCatalog;
using Pinta.Modules.Catalog.Features.Settings;
using Pinta.Modules.Catalog.Features.Snapshot;
using Pinta.Modules.Identity.Contracts;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Catalog;

public static class CatalogModule
{
    public static IHostApplicationBuilder AddCatalogModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<CatalogDbContext>(CatalogDbContext.Schema);
        builder.Services.AddScoped<IPriceSnapshotProvider, PriceSnapshotProvider>();
        builder.Services.AddScoped<ICoverageService, CoverageService>();
        builder.Services.AddScoped<IUploadLimits, UploadLimitsProvider>();
        builder.Services.AddScoped<IRequestSettings, RequestSettingsProvider>();
        builder.Services.AddScoped<IExtraItems, ExtraItemsProvider>();
        builder.Services.AddScoped<ICompanyDetails, CompanyDetailsProvider>();
        builder.Services.AddValidatorsFromAssemblyContaining<PaintRequest>(includeInternalTypes: true);
        return builder;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        GetCatalogEndpoint.Map(app);
        PublicCoverageEndpoints.Map(app);

        var catalog = app.MapGroup("/api/admin/catalog").WithTags("Admin catalog")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
        Paints.Crud.Map(catalog);
        SurfaceTypes.Crud.Map(catalog);
        RoomTypes.Crud.Map(catalog);
        Modifiers.Crud.Map(catalog);
        ExtraItems.Crud.Map(catalog);

        var admin = app.MapGroup("/api/admin").WithTags("Admin settings")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
        SettingsEndpoints.Map(admin);
        CoverageEndpoints.Map(admin);
        return app;
    }
}
