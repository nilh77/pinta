using Microsoft.EntityFrameworkCore;
using Pinta.SharedKernel.Data;

namespace Pinta.Api.Infrastructure;

public static class ModuleMigrator
{
    /// <summary>Applies the pending migrations of every module DbContext (Development only; docs/structure.md).</summary>
    public static async Task MigrateModulesAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        foreach (var registration in scope.ServiceProvider.GetServices<ModuleDbContextRegistration>())
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(registration.DbContextType);
            await context.Database.MigrateAsync();
        }
    }
}
