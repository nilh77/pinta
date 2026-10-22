using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

namespace Pinta.Api.Infrastructure;

public static class OpenApiSetup
{
    /// <summary>OpenAPI document with the <c>code</c> and <c>traceId</c> extensions every ProblemDetails carries.</summary>
    public static IServiceCollection AddPintaOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(options => options.AddSchemaTransformer((schema, context, _) =>
        {
            if (context.JsonTypeInfo.Type == typeof(ProblemDetails) || context.JsonTypeInfo.Type == typeof(HttpValidationProblemDetails))
            {
                schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
                schema.Properties["code"] = new OpenApiSchema { Type = JsonSchemaType.String };
                schema.Properties["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String };
                schema.Required ??= new HashSet<string>();
                schema.Required.Add("code");
            }

            return Task.CompletedTask;
        }));
}
