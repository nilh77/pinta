using Microsoft.EntityFrameworkCore;

namespace Pinta.SharedKernel.Data;

public static class ConcurrencyExtensions
{
    /// <summary>
    /// Saves an edit made against <paramref name="expectedVersion"/>. Returns <c>false</c> when someone else changed the
    /// row first, either before it was loaded or between loading and saving (<c>409 concurrency_conflict</c>).
    /// </summary>
    public static async Task<bool> TrySaveVersionAsync<TEntity>(
        this DbContext context, TEntity entity, uint expectedVersion, CancellationToken cancellationToken)
        where TEntity : AuditedEntity
    {
        if (entity.Version != expectedVersion)
        {
            return false;
        }

        context.Entry(entity).Property(e => e.Version).OriginalValue = expectedVersion;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
