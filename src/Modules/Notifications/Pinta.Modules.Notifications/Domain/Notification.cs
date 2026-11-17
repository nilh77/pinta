using Pinta.Modules.Notifications.Contracts;

namespace Pinta.Modules.Notifications.Domain;

/// <summary>A notice in a user's bell (docs/data-model.md, «Notificaciones»). Read ones are deleted after 90 days.</summary>
public sealed class Notification
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid UserId { get; init; }

    public NotificationKind Kind { get; init; }

    public NoticeAudience Audience { get; init; }

    public Guid? JobId { get; init; }

    public string? JobCode { get; init; }

    public Dictionary<string, string> Data { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ReadAt { get; set; }
}

/// <summary>A device and browser registered for web push; deleted when the push service reports it gone.</summary>
public sealed class PushSubscription
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public required string Endpoint { get; init; }

    public required string P256dh { get; set; }

    public required string Auth { get; set; }

    public string? UserAgent { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}
