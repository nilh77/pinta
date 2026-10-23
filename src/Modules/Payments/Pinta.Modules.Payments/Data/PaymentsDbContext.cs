using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Payments.Contracts;
using Pinta.Modules.Payments.Domain;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Payments.Data;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public const string Schema = "payments";

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Refund> Refunds => Set<Refund>();

    public DbSet<StripeEvent> StripeEvents => Set<StripeEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Payment>(payment =>
        {
            payment.ToTable("payments");
            payment.HasIndex(p => new { p.JobId, p.Type });
            payment.HasIndex(p => p.StripePaymentIntentId).IsUnique();
            payment.HasIndex(p => p.ReceiptNumber).IsUnique();
            payment.Property(p => p.Type).HasSnakeCaseConversion(20);
            payment.Property(p => p.Status).HasSnakeCaseConversion(20);
            payment.Property(p => p.Method).HasConversion(m => m == null ? null : SnakeCase(m.Value), s => s == null ? null : Enum.Parse<PaymentMethod>(s.Replace("_", ""), true)).HasMaxLength(20);
            payment.Property(p => p.Amount).HasPrecision(12, 2);
            payment.Property(p => p.StripePaymentIntentId).HasMaxLength(100);
            payment.Property(p => p.MultibancoEntity).HasMaxLength(10);
            payment.Property(p => p.MultibancoReference).HasMaxLength(20);
            payment.Property(p => p.Error).HasMaxLength(500);
            payment.Property(p => p.ReceiptNumber).HasMaxLength(20);
            payment.Property(p => p.Version).IsRowVersion();
            payment.Ignore(p => p.IsOpen);
            payment.Ignore(p => p.Refunded);
            payment.HasMany(p => p.Refunds).WithOne().HasForeignKey(r => r.PaymentId);
        });
        modelBuilder.Entity<Refund>(refund =>
        {
            refund.ToTable("refunds");
            refund.Property(r => r.Id).ValueGeneratedNever();
            refund.Property(r => r.Amount).HasPrecision(12, 2);
            refund.Property(r => r.Reason).HasMaxLength(500);
            refund.Property(r => r.Status).HasSnakeCaseConversion(20);
            refund.Property(r => r.StripeRefundId).HasMaxLength(100);
            refund.HasIndex(r => r.StripeRefundId).IsUnique();
            refund.Property(r => r.Error).HasMaxLength(500);
        });
        modelBuilder.Entity<StripeEvent>(stripeEvent =>
        {
            stripeEvent.ToTable("stripe_events");
            stripeEvent.HasKey(e => e.Id);
            stripeEvent.Property(e => e.Id).HasMaxLength(100);
            stripeEvent.Property(e => e.Type).HasMaxLength(100);
            stripeEvent.Property(e => e.Payload).HasColumnType("jsonb");
        });
        modelBuilder.Entity<ReceiptCounter>(counter =>
        {
            counter.ToTable("receipt_counters");
            counter.HasKey(c => c.Year);
            counter.Property(c => c.Year).ValueGeneratedNever();
        });
    }

    private static string SnakeCase(PaymentMethod method) => System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(method.ToString());

    /// <summary>Next <c>SIM-YYYY-NNNN</c>, one counter per year incremented atomically in PostgreSQL.</summary>
    public async Task<string> NextReceiptNumberAsync(int year, CancellationToken cancellationToken)
    {
        var last = (await Database.SqlQuery<int>($"""
            INSERT INTO payments.receipt_counters (year, last) VALUES ({year}, 1)
            ON CONFLICT (year) DO UPDATE SET last = receipt_counters.last + 1
            RETURNING last AS "Value"
            """).ToListAsync(cancellationToken)).Single();
        return $"SIM-{year}-{last:D4}";
    }
}
