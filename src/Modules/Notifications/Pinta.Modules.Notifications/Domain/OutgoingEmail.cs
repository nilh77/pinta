namespace Pinta.Modules.Notifications.Domain;

public enum OutgoingEmailStatus
{
    Queued,
    Sent,
    Failed,
}

/// <summary>
/// Every email Pinta sends (docs/glossary.md, notifications.outbox; S2): rendered once in the recipient's language and
/// sent by Hangfire with up to 5 retries. A failure never undoes the operation that queued it.
/// </summary>
public sealed class OutgoingEmail
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string To { get; set; }

    public required string Type { get; init; }

    public required string Subject { get; init; }

    public required string Html { get; set; }

    public Guid? JobId { get; init; }

    public OutgoingEmailStatus Status { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? SentAt { get; set; }
}
