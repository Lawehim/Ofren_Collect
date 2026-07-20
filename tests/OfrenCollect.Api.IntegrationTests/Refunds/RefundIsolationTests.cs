using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OfrenCollect.Api.IntegrationTests.Persistence;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Common;
using OfrenCollect.Application.Refunds.InitiateRefund;
using OfrenCollect.Domain.Payments;
using OfrenCollect.Domain.Refunds;
using OfrenCollect.Repository.Persistence;
using OfrenCollect.Repository.Persistence.Repositories;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Api.IntegrationTests.Refunds;

[Collection(nameof(PostgresCollection))]
public sealed class RefundIsolationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _fixture;

    public RefundIsolationTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InitiateRefund_ForOwnMatchedPayment_PersistsRequestedRefund()
    {
        var tenant = Guid.NewGuid();
        await SeedMatchedPayment(tenant, "MNFY-RF-1", "7080000001", 20000m);

        await using var db = AsTenant(tenant);
        var result = await CreateHandler(db, tenant).Handle(
            new InitiateRefundCommand("MNFY-RF-1", 5000m, "Overpaid", "OFREN-RF-1"), CancellationToken.None);

        result.Status.Should().Be(nameof(RefundStatus.Requested));
        (await db.Refunds.CountAsync(r => r.RefundReference == "OFREN-RF-1")).Should().Be(1);
    }

    [Fact]
    public async Task InitiateRefund_ForAnotherTenantsPayment_IsNotFound()
    {
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        await SeedMatchedPayment(owner, "MNFY-RF-2", "7080000002", 20000m);

        await using var db = AsTenant(attacker);
        var act = () => CreateHandler(db, attacker).Handle(
            new InitiateRefundCommand("MNFY-RF-2", 5000m, "Trying", "OFREN-RF-2"), CancellationToken.None);

        // Tenant B must never be able to refund Tenant A's transaction (§8).
        await act.Should().ThrowAsync<NotFoundException>();
        (await db.Refunds.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task InitiateRefund_SameReferenceTwice_IsIdempotent()
    {
        var tenant = Guid.NewGuid();
        await SeedMatchedPayment(tenant, "MNFY-RF-3", "7080000003", 20000m);
        var command = new InitiateRefundCommand("MNFY-RF-3", 5000m, "Overpaid", "OFREN-RF-3");

        await using var db = AsTenant(tenant);
        await CreateHandler(db, tenant).Handle(command, CancellationToken.None);
        await CreateHandler(db, tenant).Handle(command, CancellationToken.None);

        (await db.Refunds.CountAsync(r => r.RefundReference == "OFREN-RF-3")).Should().Be(1);
    }

    private OfrenDbContext AsTenant(Guid tenantId) => _fixture.CreateContext(new TestTenantContext(tenantId));

    private static InitiateRefundCommandHandler CreateHandler(OfrenDbContext db, Guid tenant)
    {
        var context = new TestTenantContext(tenant);
        var monnify = Substitute.For<IMonnifyRefundClient>();
        monnify.InitiateRefundAsync(Arg.Any<RefundInitiationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RefundInitiationResult(MonnifyRefundStatus.Pending));

        return new InitiateRefundCommandHandler(
            new RefundRepository(db),
            new PaymentEventRepository(db, context),
            monnify,
            new UnitOfWork(db),
            context,
            new FixedClock(Now));
    }

    private async Task SeedMatchedPayment(Guid tenantId, string reference, string account, decimal amount)
    {
        await using var db = AsTenant(tenantId);
        db.PaymentEvents.Add(PaymentEvent.RecordMatched(
            tenantId, reference, account, Money.Of(amount), Now, Guid.NewGuid()));
        await db.SaveChangesAsync();
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
