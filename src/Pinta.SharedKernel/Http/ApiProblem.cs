using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Pinta.SharedKernel.Http;

/// <summary>Builds ProblemDetails results with a stable <c>code</c>; the API host adds the translated title and <c>traceId</c>.</summary>
public static class ApiProblem
{
    public const string CodeKey = "code";

    public static ProblemHttpResult Create(int status, string code) =>
        TypedResults.Problem(new ProblemDetails
        {
            Status = status,
            Extensions = { [CodeKey] = code },
        });

    public static ProblemHttpResult Validation(IDictionary<string, string[]> errors) =>
        TypedResults.Problem(new HttpValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Type = "https://pinta.pt/errors/validation",
            Extensions = { [CodeKey] = ErrorCodes.ValidationFailed },
        });
}
