using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Painters.Data;
using Pinta.SharedKernel.Http;
using Pinta.SharedKernel.Time;

namespace Pinta.Modules.Painters.Features;

/// <summary>Mondays at 09:00 Lisbon (S3): one email to the admins with the painter documents expiring within 30 days.</summary>
public sealed class PainterDocumentAlerts(PaintersDbContext db, IUserDirectory users, IEmailQueue emails, WebLinks links, TimeProvider clock)
{
    public const string JobId = "painter-document-alerts";

    public async Task<int> RunAsync()
    {
        var today = BusinessDays.Today(clock);
        var limit = today.AddDays(30);
        var expiring = await (from d in db.Documents.AsNoTracking()
                              join p in db.Painters.AsNoTracking() on d.PainterId equals p.Id
                              where d.ExpiresOn != null && d.ExpiresOn >= today && d.ExpiresOn <= limit
                              orderby d.ExpiresOn
                              select new { p.Name, d.Kind, d.ExpiresOn }).ToListAsync();
        if (expiring.Count > 0)
        {
            var lines = expiring.Select(e => $"{e.Name} · {e.Kind} · {e.ExpiresOn:yyyy-MM-dd}").ToList();
            foreach (var admin in await users.ListAdminsAsync())
            {
                await emails.EnqueueAsync(new PainterDocumentsExpiringEmail(admin.Email, admin.Locale, lines, links.AdminPainters(admin.Locale)));
            }
        }

        return expiring.Count;
    }
}
