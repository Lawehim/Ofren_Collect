using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OfrenCollect.Api.IntegrationTests.Persistence;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Refunds.ResolveRefund;
using OfrenCollect.Domain.Refunds;
using OfrenCollect.Repository.Persistence;
using OfrenCollect.Repository.Persistence.Repositories;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Api.IntegrationTests.Refunds;

[Collection(nameof(PostgresCollection))]
public sealed class RefundResolutionTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 13, 0, 0, TimeSpan.Zero);
    private const string RefundRef = "OFREN-RES-1";

    private readonly PostgresFixture _fixture;

    public RefundResolutionTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ResolveRefund_OnWebhookPath_WithNoAmbientTenant_MarksRefundCompleted()
    {
        var tenant = Guid.NewGuid();
        await using (var db = _fixture.CreateContext(new TestTenantContext(tenant)))
        {
            db.Refunds.Add(Refund.Request(
                tenant, "MNFY-ORIG-1", RefundRef, Money.Of(5000m), "Overpaid", Money.Of(25000m), Now));
            await db.SaveChangesAsync();
        }

        // Webhook path: the drainer resolves with NO ambient tenant. The refund is tenant-owned, so
        // this only works because the repository bypasses the global filter (FR-11.4). Monnify is
        // faked to confirm COMPLETED — the handler re-verifies rather than trusting the webhook (§8).
        var monnify = Substitute.For<IMonnifyRefundClient>();
        monnify.GetRefundStatusAsync(RefundRef, Arg.Any<CancellationToken>()).Returns(MonnifyRefundStatus.Completed);
        await using (var db = _fixture.CreateContext(new TestTenantContext(null)))
        {
            var handler = new ResolveRefundCommandHandler(
                new RefundRepository(db), monnify, new UnitOfWork(db), new FixedClock(Now));
            await handler.Handle(new ResolveRefundCommand(RefundRef), CancellationToken.None);
        }

        await using (var verify = _fixture.CreateContext(new TestTenantContext(null)))
        {
            var refund = await verify.Refunds.IgnoreQueryFilters().SingleAsync(r => r.RefundReference == RefundRef);
            refund.Status.Should().Be(RefundStatus.Completed);
            refund.ResolvedAt.Should().Be(Now);
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
