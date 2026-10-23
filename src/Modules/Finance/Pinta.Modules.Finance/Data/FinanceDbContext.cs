using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Finance.Domain;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Finance.Data;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public const string Schema = "finance";

    public DbSet<PainterPayout> PainterPayouts => Set<PainterPayout>();

    public DbSet<OperatingCost> OperatingCosts => Set<OperatingCost>();

    public DbSet<MonthlyClosing> MonthlyClosings => Set<MonthlyClosing>();

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
        modelBuilder.Entity<OperatingCost>(cost =>
        {
            cost.ToTable("operating_costs");
            cost.Property(c => c.Id).ValueGeneratedNever();
            cost.Property(c => c.Category).HasSnakeCaseConversion(30);
            cost.Property(c => c.Description).HasMaxLength(200);
            cost.Property(c => c.Amount).HasPrecision(12, 2);
            cost.Property(c => c.Version).IsRowVersion();
            cost.HasIndex(c => c.Date);
            cost.HasOne<MonthlyClosing>().WithMany().HasForeignKey(c => c.ClosingId);
        });
        modelBuilder.Entity<MonthlyClosing>(closing =>
        {
            closing.ToTable("monthly_closings");
            closing.Property(c => c.Id).ValueGeneratedNever();
            foreach (var amount in new[] { "Income", "Vat", "Material", "PainterPay", "GrossMargin", "Costs", "Profit", "PerPartner" })
            {
                closing.Property<decimal>(amount).HasPrecision(12, 2);
            }

            // A month closes once.
            closing.HasIndex(c => c.Period).IsUnique();
        });
    }
}
