using System.Diagnostics;
using System.Globalization;
using System.Resources;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Api.Infrastructure;

/// <summary>Every error is ProblemDetails with a stable <c>code</c>, a title in the request language and the <c>traceId</c> (docs/api.md).</summary>
public static class ProblemDetailsSetup
{
    private static readonly ResourceManager Titles = new("Pinta.Api.Resources.ErrorTitles", typeof(ProblemDetailsSetup).Assembly);

    public static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;
        if (!problem.Extensions.TryGetValue(ApiProblem.CodeKey, out var value) || value is not string code)
        {
            code = ErrorCodes.ForStatus(status);
            problem.Extensions[ApiProblem.CodeKey] = code;
        }

        problem.Title = Titles.GetString(code, CultureInfo.CurrentUICulture) ?? problem.Title;
        if (problem.Type?.StartsWith("https://pinta.pt/", StringComparison.Ordinal) != true)
        {
            problem.Type = $"https://pinta.pt/errors/{code}";
        }

        problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
    }
}
