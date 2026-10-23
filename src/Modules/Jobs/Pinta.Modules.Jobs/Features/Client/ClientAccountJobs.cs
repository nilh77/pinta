using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Domain;
using Pinta.Modules.Jobs.Features.Shared;
using Pinta.Modules.Media.Contracts;

namespace Pinta.Modules.Jobs.Features.Client;

internal sealed class ClientAccountJobs(JobsDbContext db, JobCancellation cancellation, IMediaService media, TimeProvider clock) : IClientAccountJobs
{
    private const string Removed = "—";

    public async Task<IReadOnlyList<ClientJobForDeletion>> ForDeletionAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var result = new List<ClientJobForDeletion>();
        foreach (var job in await db.Jobs.AsNoTracking().Where(j => j.ClientId == clientId).OrderBy(j => j.CreatedAt).ToListAsync(cancellationToken))
        {
            var action = ActionFor(job.Status);
            var refund = action == DeletionAction.Cancel ? (await cancellation.PreviewAsync(job, false, cancellationToken)).RefundCents : 0;
            result.Add(new ClientJobForDeletion(job.Id, job.Code, job.Status, action, refund));
        }

        return result;
    }

    public async Task<bool> RemoveClientAsync(Guid clientId, Guid adminId, CancellationToken cancellationToken = default)
    {
        var jobs = await db.Jobs.Where(j => j.ClientId == clientId).ToListAsync(cancellationToken);
        if (jobs.Any(j => ActionFor(j.Status) == DeletionAction.Blocked))
        {
            return false;
        }

        foreach (var job in jobs.Where(j => ActionFor(j.Status) == DeletionAction.Cancel))
        {
            var preview = await cancellation.PreviewAsync(job, false, cancellationToken);
            job.TransitionTo(JobStatus.Cancelled, JobActor.Admin, adminId, "Supresión de la cuenta", clock.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            await cancellation.CancelledAsync(job, preview, null, cancellationToken);
        }

        foreach (var job in jobs)
        {
            job.AddressLine1 = Removed;
            job.Floor = null;
            job.AccessNotes = null;
            job.Description = null;
            job.TaxId = null;
            job.CompanyName = null;
            // Photos and videos of the client's home; the painter's result and visit photos are Pinta's records.
            foreach (var file in (await media.ListForJobAsync(job.Id, cancellationToken)).Where(f => f.Context is MediaContext.Element or MediaContext.JobGeneral))
            {
                await media.DeleteAsync(file.Id, cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static DeletionAction ActionFor(JobStatus status) => status switch
    {
        JobStatus.InProgress or JobStatus.Finished => DeletionAction.Blocked,
        _ when JobStateMachine.CancellableFrom.Contains(status) => DeletionAction.Cancel,
        _ => DeletionAction.Keep,
    };
}
