using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Finance.Domain;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Finance.Data;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public const string Schema = "finance";

    public DbSet<PainterPayout> PainterPayouts => Set<PainterPayout>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<PainterPayout>(payout =>
        {
            payout.ToTable("painter_payouts");
            payout.Property(p => p.Id).ValueGeneratedNever();
            payout.Property(p => p.JobCode).HasMaxLength(20);
            payout.Property(p => p.Amount).HasPrecision(12, 2);
            payout.Property(p => p.Status).HasSnakeCaseConversion(20);
            payout.Property(p => p.Reference).HasMaxLength(100);
            payout.Property(p => p.Version).IsRowVersion();
            // One payout per job: a repeated JobClosed creates nothing.
            payout.HasIndex(p => p.JobId).IsUnique();
            payout.HasIndex(p => new { p.PainterId, p.Status });
        });
    }
}
