using System.Reflection;

namespace Pinta.Api.Infrastructure;

public static class OpenApiGeneration
{
    /// <summary>True while Microsoft.Extensions.ApiDescription.Server runs the app on build to write openapi/Pinta.Api.json.</summary>
    public static bool IsRunning => Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
