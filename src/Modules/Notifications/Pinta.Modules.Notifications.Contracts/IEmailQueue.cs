namespace Pinta.Modules.Notifications.Contracts;

/// <summary>Queues a transactional email; sending runs in the background with retries and never fails the caller (S2).</summary>
public interface IEmailQueue
{
    Task EnqueueAsync(Email email, CancellationToken cancellationToken = default);
}
