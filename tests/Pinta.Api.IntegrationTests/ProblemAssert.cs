using System.Net;
using System.Text.Json;

namespace Pinta.Api.IntegrationTests;

public static class ProblemAssert
{
    /// <summary>Asserts an RFC 9457 response with the stable <c>code</c>, a title and a <c>traceId</c> (docs/api.md).</summary>
    public static async Task<JsonElement> HasCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadJsonAsync();
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        return problem;
    }
}
