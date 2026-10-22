namespace Pinta.Modules.Notifications.Contracts;

/// <summary>An email recorded in the outbox (S2), as the job history shows it (A3.5).</summary>
public sealed record SentEmail(string Type, string To, string Subject, string Status, int Attempts, DateTimeOffset CreatedAt, DateTimeOffset? SentAt);

public interface IEmailLog
{
    Task<IReadOnlyList<SentEmail>> ListForJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}
