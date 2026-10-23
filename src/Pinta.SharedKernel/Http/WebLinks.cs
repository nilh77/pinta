using Microsoft.Extensions.Configuration;

namespace Pinta.SharedKernel.Http;

/// <summary>Links to the web app for emails (<c>Web:BaseUrl</c>; the web app proxies /api to the API).</summary>
public sealed class WebLinks(IConfiguration configuration)
{
    private string Web => (configuration["Web:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');

    public string ClientJob(Guid jobId, Locale locale) => $"{Web}/{Lang(locale)}/mis-trabajos/{jobId}";

    public string AdminJob(Guid jobId, Locale locale) => $"{Web}/{Lang(locale)}/admin/trabajos/{jobId}";

    private static string Lang(Locale locale) => locale.ToString().ToLowerInvariant();
}
