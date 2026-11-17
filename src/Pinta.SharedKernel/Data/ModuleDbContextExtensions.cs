using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Pinta.SharedKernel.Data;

/// <summary>Marks a module DbContext so the host can migrate every module at startup.</summary>
public sealed record ModuleDbContextRegistration(Type DbContextType);

public static class ModuleDbContextExtensions
{
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>
    /// Registers a module DbContext on the shared PostgreSQL data source, with snake_case names
    /// and its migrations history inside its own schema (one schema per module).
    /// </summary>
    /// <param name="interceptors">Per-scope interceptors of this context (Jobs raises its notices with one).</param>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services, string schema, Func<IServiceProvider, IInterceptor[]>? interceptors = null)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>((sp, options) => options
            .UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql
                .MigrationsHistoryTable(MigrationsHistoryTable, schema)
                .MigrationsAssembly(typeof(TContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptors?.Invoke(sp) ?? []));
        services.AddSingleton(new ModuleDbContextRegistration(typeof(TContext)));
        return services;
    }
}
