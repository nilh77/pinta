namespace Pinta.Modules.Jobs.Domain;

/// <summary>The accepted offer's immutable PDF and its eventual receipt.</summary>
public sealed class JobProtocol
{
    public Guid JobId { get; init; }
    public Guid PainterId { get; init; }
    public required string JobCode { get; init; }
    public required byte[] Pdf { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public DateTimeOffset? ReceivedAt { get; private set; }
    public Guid? ReceivedByUserId { get; private set; }
    public string? ReceivedBy { get; private set; }
    public Guid[] FileIds { get; private set; } = [];
    public uint Version { get; private set; }

    public void RecordReceipt(Guid userId, string name, DateTimeOffset receivedAt, Guid[] fileIds)
    {
        ReceivedByUserId = userId;
        ReceivedBy = name;
        ReceivedAt = receivedAt;
        FileIds = fileIds;
    }
}
