namespace Pinta.Api.Infrastructure;

/// <summary>
/// The environments that migrate at startup, seed the demo and offer «Restablecer datos de demo»: Development (local) and
/// Demo (Railway). Never Production (docs/architecture.md, "Entornos").
/// </summary>
public static class DemoEnvironment
{
    public const string Name = "Demo";

    public static bool HasDemoData(this IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment(Name);
}
