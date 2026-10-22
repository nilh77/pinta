namespace Pinta.Modules.Catalog.Contracts;

/// <summary>Global settings that apply to a client request (A1.6): date notice and review deadline in working days.</summary>
public sealed record RequestSettings(int MinLeadBusinessDays, int ReviewDeadlineBusinessDays);

public interface IRequestSettings
{
    Task<RequestSettings> GetAsync(CancellationToken cancellationToken = default);
}
