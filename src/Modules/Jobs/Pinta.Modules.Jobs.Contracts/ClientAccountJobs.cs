namespace Pinta.Modules.Jobs.Contracts;

/// <summary>
/// What removing a client does to each job (C5.8): <c>Cancel</c> it with the usual refund policy, <c>Blocked</c> while the
/// work is in progress or its balance is due, or <c>Keep</c> it (already over) with its personal data removed.
/// </summary>
public enum DeletionAction
{
    Cancel,
    Blocked,
    Keep,
}

public sealed record ClientJobForDeletion(Guid JobId, string Code, JobStatus Status, DeletionAction Action, long RefundCents);

/// <summary>The Jobs side of an account deletion executed by the admin (C5.8).</summary>
public interface IClientAccountJobs
{
    Task<IReadOnlyList<ClientJobForDeletion>> ForDeletionAsync(Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the client's cancellable jobs with the refund policy, then removes the personal data of every job (street
    /// address, floor, access notes, description, NIF, company) and the photos the client uploaded. Codes, amounts and
    /// history stay for the accounts. False, and nothing done, while a job is <see cref="DeletionAction.Blocked"/>.
    /// </summary>
    Task<bool> RemoveClientAsync(Guid clientId, Guid adminId, CancellationToken cancellationToken = default);
}
