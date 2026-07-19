using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Domain.Customers;
using OfrenCollect.Domain.Invoices;
using OfrenCollect.Domain.Plans;
using OfrenCollect.Domain.Subscriptions;
using OfrenCollect.Domain.Tenants;
using OfrenCollect.Domain.Users;
using OfrenCollect.Repository.Persistence;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Api.Persistence;

/// <summary>
/// Seeds demo data on first run so the app is never empty when a judge opens it (NFR-3.2):
/// a tenant with an owner login (ada@brightpath.ng / password123), a plan, a customer, and a
/// subscription with a reserved account and its first invoice.
/// </summary>
public sealed class DatabaseSeeder
{
    private const string SeedReservedAccountReference = "OFREN-SEED-0001";

    private readonly OfrenDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _clock;

    public DatabaseSeeder(OfrenDbContext db, IPasswordHasher passwordHasher, TimeProvider clock)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _clock = clock;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await _db.Tenants.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = _clock.GetUtcNow();

        var tenant = Tenant.Register("BrightPath Tutors", now);
        var owner = User.Create(
            tenant.Id, "ada@brightpath.ng", _passwordHasher.Hash("password123"), UserRole.Owner);
        var plan = Plan.Create(tenant.Id, "Premium", Money.Of(25000m), BillingInterval.Monthly);
        var customer = Customer.Register(tenant.Id, "Chidi Eze", "chidi@mail.com");

        var nextDueDate = plan.Interval.NextDueDateFrom(now);
        var subscription = Subscription.Enrol(
            tenant.Id, customer.Id, plan.Id, SeedReservedAccountReference, nextDueDate);
        subscription.AttachReservedAccount("7080124933", "Wema Bank");
        var invoice = Invoice.Create(tenant.Id, subscription.Id, plan.Amount, now, nextDueDate);

        // No ambient tenant during seeding, so entities keep the TenantId set here.
        _db.AddRange(tenant, owner, plan, customer, subscription, invoice);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
