using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;

namespace Pinta.Modules.Jobs.Features.Access;

internal sealed class JobAccess(JobsDbContext db) : IJobAccess
{
    public Task<bool> IsClientOfAsync(Guid jobId, Guid userId, CancellationToken cancellationToken = default) =>
        db.Jobs.AnyAsync(j => j.Id == jobId && j.ClientId == userId, cancellationToken);
}
