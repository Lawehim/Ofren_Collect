using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Domain.Abstractions;
using OfrenCollect.Domain.Audit;
using OfrenCollect.Domain.Customers;
using OfrenCollect.Domain.Invoices;
using OfrenCollect.Domain.Mandates;
using OfrenCollect.Domain.Payments;
using OfrenCollect.Domain.Plans;
using OfrenCollect.Domain.Refunds;
using OfrenCollect.Domain.Subscriptions;
using OfrenCollect.Domain.Tenants;
using OfrenCollect.Domain.Users;
using OfrenCollect.Domain.Webhooks;

namespace OfrenCollect.Repository.Persistence;

/// <summary>
/// The EF Core context. Two isolation guarantees are enforced here, not left to callers:
/// a global query filter scopes every read of a tenant-owned entity to the current tenant,
/// and <see cref="SaveChangesAsync"/> stamps the tenant on new tenant-owned rows so it can be
/// neither forgotten nor forged (NFR-1.7, CLAUDE.md §8 / §11.3).
/// </summary>
public sealed class OfrenDbContext : DbContext
{
    private const int ShortText = 200;
    private const int CurrencyCodeLength = 3;
    private const int MoneyPrecision = 18;
    private const int MoneyScale = 2;

    private readonly ITenantContext _tenantContext;

    public OfrenDbContext(DbContextOptions<OfrenDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Mandate> Mandates => Set<Mandate>();
    public DbSet<MandateDebit> MandateDebits => Set<MandateDebit>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenantOnNewEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampTenantOnNewEntities();
        return base.SaveChanges();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureTenant(modelBuilder);
        ConfigureUser(modelBuilder);
        ConfigurePlan(modelBuilder);
        ConfigureCustomer(modelBuilder);
        ConfigureSubscription(modelBuilder);
        ConfigureInvoice(modelBuilder);
        ConfigurePaymentEvent(modelBuilder);
        ConfigureRefund(modelBuilder);
        ConfigureMandate(modelBuilder);
        ConfigureMandateDebit(modelBuilder);
        ConfigureAuditEntry(modelBuilder);
        ConfigureInboxMessage(modelBuilder);
    }

    private static void ConfigureInboxMessage(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.HasKey(m => m.Id);
            b.Property(m => m.EventType).HasConversion<string>().HasMaxLength(ShortText);
            // Nullable: which references are populated depends on the event type.
            b.Property(m => m.TransactionReference).HasMaxLength(ShortText);
            b.Property(m => m.DestinationAccountNumber).HasMaxLength(ShortText);
            b.Property(m => m.RefundReference).HasMaxLength(ShortText);
            b.Property(m => m.MandateReference).HasMaxLength(ShortText);
            // The webhook's claimed outcome is intentionally not stored: refund and mandate statuses
            // are re-verified with Monnify, never taken from the webhook body (§8, FR-9.2, FR-11.4).
            b.Property(m => m.RawPayload).IsRequired();
            b.HasIndex(m => m.ProcessedAt);
        });

    private static void ConfigureAuditEntry(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<AuditEntry>(b =>
        {
            b.HasKey(a => a.Id);
            b.Property(a => a.CorrelationId).HasMaxLength(ShortText).IsRequired();
            b.Property(a => a.Method).HasMaxLength(16).IsRequired();
            b.Property(a => a.Path).HasMaxLength(ShortText).IsRequired();
            b.Property(a => a.QueryString).HasMaxLength(ShortText);
            b.Property(a => a.IpAddress).HasMaxLength(64);
            // Audit is nullable-tenant (pre-auth calls) and so is not covered by the global
            // filter; the audit query scopes by tenant explicitly.
            b.HasIndex(a => new { a.TenantId, a.TimestampUtc });
        });

    private static void ConfigureTenant(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Tenant>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.BusinessName).HasMaxLength(ShortText).IsRequired();
        });

    private void ConfigureUser(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);
            b.Property(u => u.Email).HasMaxLength(ShortText).IsRequired();
            b.Property(u => u.PasswordHash).IsRequired();
            b.Property(u => u.Role).HasConversion<string>().HasMaxLength(ShortText);
            b.Property(u => u.PasswordResetTokenHash).HasMaxLength(ShortText);
            b.HasIndex(u => u.Email).IsUnique();
            b.HasIndex(u => u.PasswordResetTokenHash);
            ApplyTenantFilter(b);
        });

    private void ConfigurePlan(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Plan>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).HasMaxLength(ShortText).IsRequired();
            b.Property(p => p.Interval).HasConversion<string>().HasMaxLength(ShortText);
            ConfigureMoney(b.ComplexProperty(p => p.Amount), "Amount");
            ApplyTenantFilter(b);
        });

    private void ConfigureCustomer(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Customer>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).HasMaxLength(ShortText).IsRequired();
            b.Property(c => c.Email).HasMaxLength(ShortText).IsRequired();
            ApplyTenantFilter(b);
        });

    private void ConfigureSubscription(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Subscription>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.ReservedAccountReference).HasMaxLength(ShortText).IsRequired();
            b.Property(s => s.ReservedAccountNumber).HasMaxLength(ShortText);
            b.Property(s => s.ReservedBankName).HasMaxLength(ShortText);
            b.Property(s => s.Status).HasConversion<string>().HasMaxLength(ShortText);
            b.HasIndex(s => s.ReservedAccountReference).IsUnique();
            b.HasIndex(s => s.ReservedAccountNumber).IsUnique();
            ApplyTenantFilter(b);
        });

    private void ConfigureInvoice(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Invoice>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.Status).HasConversion<string>().HasMaxLength(ShortText);
            ConfigureMoney(b.ComplexProperty(i => i.AmountDue), "AmountDue");
            ConfigureMoney(b.ComplexProperty(i => i.AmountPaid), "AmountPaid");
            b.Ignore(i => i.OutstandingBalance);
            b.Ignore(i => i.Credit);
            ApplyTenantFilter(b);
        });

    private static void ConfigurePaymentEvent(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<PaymentEvent>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.MonnifyTransactionReference).HasMaxLength(ShortText).IsRequired();
            b.Property(p => p.ReservedAccountNumber).HasMaxLength(ShortText).IsRequired();
            ConfigureMoney(b.ComplexProperty(p => p.Amount), "Amount");
            b.Ignore(p => p.IsMatched);
            // The Monnify reference is the idempotency key (FR-3.6, NFR-2.1).
            b.HasIndex(p => p.MonnifyTransactionReference).IsUnique();
            // PaymentEvent carries a nullable tenant (unmatched inflows have none) and is
            // deliberately not covered by the global tenant filter; its reads scope explicitly.
        });

    private void ConfigureMandate(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Mandate>(b =>
        {
            b.HasKey(m => m.Id);
            b.Property(m => m.MandateReference).HasMaxLength(ShortText).IsRequired();
            b.Property(m => m.MonnifyMandateCode).HasMaxLength(ShortText).IsRequired();
            b.Property(m => m.Status).HasConversion<string>().HasMaxLength(ShortText);
            // The mandate reference is the idempotency key (FR-9).
            b.HasIndex(m => m.MandateReference).IsUnique();
            b.HasIndex(m => m.SubscriptionId);
            ApplyTenantFilter(b);
        });

    private void ConfigureMandateDebit(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<MandateDebit>(b =>
        {
            b.HasKey(d => d.Id);
            b.Property(d => d.MandateReference).HasMaxLength(ShortText).IsRequired();
            b.Property(d => d.PaymentReference).HasMaxLength(ShortText).IsRequired();
            b.Property(d => d.TransactionReference).HasMaxLength(ShortText).IsRequired();
            b.Property(d => d.Status).HasConversion<string>().HasMaxLength(ShortText);
            ConfigureMoney(b.ComplexProperty(d => d.Amount), "Amount");
            // The payment reference is the idempotency key (FR-9.3).
            b.HasIndex(d => d.PaymentReference).IsUnique();
            ApplyTenantFilter(b);
        });

    private void ConfigureRefund(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Refund>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.OriginalTransactionReference).HasMaxLength(ShortText).IsRequired();
            b.Property(r => r.RefundReference).HasMaxLength(ShortText).IsRequired();
            b.Property(r => r.Reason).HasMaxLength(ShortText).IsRequired();
            b.Property(r => r.Status).HasConversion<string>().HasMaxLength(ShortText);
            ConfigureMoney(b.ComplexProperty(r => r.Amount), "Amount");
            b.HasIndex(r => r.OriginalTransactionReference);
            // The refund reference is the idempotency key (FR-11.3).
            b.HasIndex(r => r.RefundReference).IsUnique();
            ApplyTenantFilter(b);
        });

    private static void ConfigureMoney(
        Microsoft.EntityFrameworkCore.Metadata.Builders.ComplexPropertyBuilder<OfrenCollect.SharedKernel.Money> money,
        string columnPrefix)
    {
        money.Property(m => m.Amount)
            .HasColumnName(columnPrefix)
            .HasPrecision(MoneyPrecision, MoneyScale);
        money.Property(m => m.Currency)
            .HasColumnName($"{columnPrefix}Currency")
            .HasConversion<string>()
            .HasMaxLength(CurrencyCodeLength);
    }

    private void ApplyTenantFilter<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder)
        where TEntity : class, ITenantOwned
    {
        builder.HasIndex(nameof(ITenantOwned.TenantId));
        builder.HasQueryFilter(e => e.TenantId == _tenantContext.CurrentTenantId);
    }

    private void StampTenantOnNewEntities()
    {
        if (_tenantContext.CurrentTenantId is not { } tenantId)
        {
            // No ambient tenant (e.g. the webhook path): trust the tenant the domain set.
            return;
        }

        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(ITenantOwned.TenantId)).CurrentValue = tenantId;
            }
        }
    }
}
