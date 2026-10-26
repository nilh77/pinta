namespace Pinta.Modules.Notifications.Contracts;

/// <summary>An email recorded in the outbox (S2), as the job history shows it (A3.5).</summary>
public sealed record SentEmail(string Type, string To, string Subject, string Status, int Attempts, DateTimeOffset CreatedAt, DateTimeOffset? SentAt);

public interface IEmailLog
{
    Task<IReadOnlyList<SentEmail>> ListForJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// C5.8: the emails already sent (or failed) to <paramref name="email"/> keep their type, subject and dates but get
    /// <paramref name="replacement"/> as address and lose their content. Queued ones still go out to the person.
    /// </summary>
    Task<int> AnonymizeRecipientAsync(string email, string replacement, CancellationToken cancellationToken = default);
}
