using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Domain.Abstractions;
using OfrenCollect.Domain.Customers;
using OfrenCollect.Domain.Invoices;
using OfrenCollect.Domain.Payments;
using OfrenCollect.Domain.Plans;
using OfrenCollect.Domain.Subscriptions;
using OfrenCollect.Domain.Tenants;
using OfrenCollect.Domain.Users;

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
    }

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
            b.HasIndex(u => u.Email).IsUnique();
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
