using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Painters.Contracts;
using Pinta.Modules.Painters.Data;
using Pinta.Modules.Painters.Domain;
using Pinta.SharedKernel.Events;

namespace Pinta.Modules.Painters.Features;

internal sealed class PainterDirectory(PaintersDbContext db) : IPainterDirectory
{
    public async Task<PainterSummary?> FindAsync(Guid painterId, CancellationToken cancellationToken = default) =>
        ToSummary(await db.Painters.AsNoTracking().SingleOrDefaultAsync(p => p.Id == painterId, cancellationToken));

    public async Task<PainterSummary?> FindByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        ToSummary(await db.Painters.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken));

    public async Task<IReadOnlyList<PainterSummary>> FindManyAsync(IReadOnlyCollection<Guid> painterIds, CancellationToken cancellationToken = default) =>
        [.. (await db.Painters.AsNoTracking().Where(p => painterIds.Contains(p.Id)).ToListAsync(cancellationToken)).Select(p => ToSummary(p)!)];

    public async Task<IReadOnlyList<PainterSummary>> CoveringAsync(string municipality, CancellationToken cancellationToken = default) =>
        [.. (await db.Painters.AsNoTracking()
                .Where(p => p.Status == PainterStatus.Active && p.Municipalities.Contains(municipality))
                .OrderByDescending(p => p.Rating).ThenBy(p => p.Name)
                .ToListAsync(cancellationToken))
            .Select(p => ToSummary(p)!)];

    private static PainterSummary? ToSummary(Painter? p) =>
        p is null ? null : new PainterSummary(p.Id, p.UserId, p.Name, p.Email, p.Phone, p.Status, p.Municipalities, p.Rating);
}

/// <summary>The painter set the password: invited becomes active (A4.2).</summary>
internal sealed class ActivateOnSignUp(PaintersDbContext db) : IIntegrationEventHandler<PainterSignedUp>
{
    public async Task HandleAsync(PainterSignedUp signedUp, CancellationToken cancellationToken)
    {
        var painter = await db.Painters.SingleOrDefaultAsync(p => p.UserId == signedUp.UserId, cancellationToken);
        if (painter is { Status: PainterStatus.Invited })
        {
            painter.Status = PainterStatus.Active;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
