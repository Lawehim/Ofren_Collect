using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OfrenCollect.Domain.Payments;
using OfrenCollect.Domain.Plans;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Api.IntegrationTests.Persistence;

[Collection(nameof(PostgresCollection))]
public sealed class TenantIsolationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public TenantIsolationTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GlobalQueryFilter_ReturnsOnlyCurrentTenantsRows()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedPlan(tenantA, "A-Basic", 5000m);
        await SeedPlan(tenantB, "B-Basic", 9000m);

        await using var asTenantA = _fixture.CreateContext(new TestTenantContext(tenantA));
        var plans = await asTenantA.Plans.ToListAsync();

        plans.Should().ContainSingle();
        plans[0].Name.Should().Be("A-Basic");
        plans[0].TenantId.Should().Be(tenantA);
    }

    [Fact]
    public async Task FetchingAnotherTenantsRowById_ReturnsNothing()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var planAId = await SeedPlan(tenantA, "A-Basic", 5000m);

        await using var asTenantB = _fixture.CreateContext(new TestTenantContext(tenantB));
        var found = await asTenantB.Plans.SingleOrDefaultAsync(p => p.Id == planAId);

        found.Should().BeNull();
    }

    [Fact]
    public async Task SaveChanges_StampsTenantFromContext_IgnoringForgedValue()
    {
        var realTenant = Guid.NewGuid();
        var forgedTenant = Guid.NewGuid();

        await using (var db = _fixture.CreateContext(new TestTenantContext(realTenant)))
        {
            // The entity is built claiming a different tenant; the context must override it.
            db.Plans.Add(Plan.Create(forgedTenant, "Basic", Money.Of(5000m), BillingInterval.Monthly));
            await db.SaveChangesAsync();
        }

        await using (var asReal = _fixture.CreateContext(new TestTenantContext(realTenant)))
        {
            (await asReal.Plans.SingleAsync()).TenantId.Should().Be(realTenant);
        }

        await using (var asForged = _fixture.CreateContext(new TestTenantContext(forgedTenant)))
        {
            (await asForged.Plans.AnyAsync()).Should().BeFalse();
        }
    }

    [Fact]
    public async Task Money_RoundTripsThroughPostgres_PreservingKobo()
    {
        var tenant = Guid.NewGuid();
        var planId = await SeedPlan(tenant, "Premium", 4999.99m);

        await using var db = _fixture.CreateContext(new TestTenantContext(tenant));
        var plan = await db.Plans.SingleAsync(p => p.Id == planId);

        plan.Amount.Should().Be(Money.Of(4999.99m));
    }

    [Fact]
    public async Task DuplicateMonnifyReference_ViolatesUniqueConstraint()
    {
        var tenant = Guid.NewGuid();
        const string reference = "MNFY-DUP-1";

        await using var db = _fixture.CreateContext(new TestTenantContext(tenant));
        db.PaymentEvents.Add(
            PaymentEvent.RecordUnmatched(reference, "7080000001", Money.Of(5000m), Instant()));
        await db.SaveChangesAsync();

        db.PaymentEvents.Add(
            PaymentEvent.RecordUnmatched(reference, "7080000002", Money.Of(6000m), Instant()));
        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private static DateTimeOffset Instant() => new(2026, 7, 17, 9, 13, 0, TimeSpan.Zero);

    private async Task<Guid> SeedPlan(Guid tenantId, string name, decimal amount)
    {
        await using var db = _fixture.CreateContext(new TestTenantContext(tenantId));
        var plan = Plan.Create(tenantId, name, Money.Of(amount), BillingInterval.Monthly);
        db.Plans.Add(plan);
        await db.SaveChangesAsync();
        return plan.Id;
    }
}
