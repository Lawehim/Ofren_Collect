using FluentAssertions;
using OfrenCollect.Domain.Refunds;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.UnitTests.Refunds;

public class RefundTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset RequestedAt = new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
    private const string OriginalRef = "MNFY-ORIG-001";
    private const string RefundRef = "OFREN-RFND-001";
    private const string Reason = "Customer overpaid the July invoice";

    private static Refund Request(decimal amount = 5000m, decimal maxRefundable = 25000m) =>
        Refund.Request(TenantId, OriginalRef, RefundRef, Money.Of(amount), Reason, Money.Of(maxRefundable), RequestedAt);

    [Fact]
    public void Request_WithValidAmount_CreatesRequestedRefundStampedToTenant()
    {
        var refund = Request(amount: 5000m);

        refund.TenantId.Should().Be(TenantId);
        refund.OriginalTransactionReference.Should().Be(OriginalRef);
        refund.RefundReference.Should().Be(RefundRef);
        refund.Amount.Should().Be(Money.Of(5000m));
        refund.Reason.Should().Be(Reason);
        refund.Status.Should().Be(RefundStatus.Requested);
        refund.RequestedAt.Should().Be(RequestedAt);
        refund.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void Request_AmountEqualToRefundable_IsAllowed()
    {
        var act = () => Request(amount: 25000m, maxRefundable: 25000m);

        act.Should().NotThrow();
    }

    [Fact]
    public void Request_AmountExceedingRefundable_Throws()
    {
        var act = () => Request(amount: 25001m, maxRefundable: 25000m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Request_ZeroAmount_Throws()
    {
        var act = () => Request(amount: 0m);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("", RefundRef, Reason)]
    [InlineData(OriginalRef, "", Reason)]
    [InlineData(OriginalRef, RefundRef, "")]
    public void Request_WithBlankReferenceOrReason_Throws(string originalRef, string refundRef, string reason)
    {
        var act = () => Refund.Request(
            TenantId, originalRef, refundRef, Money.Of(5000m), reason, Money.Of(25000m), RequestedAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkCompleted_FromRequested_SetsCompletedWithTimestamp()
    {
        var refund = Request();
        var completedAt = RequestedAt.AddMinutes(3);

        refund.MarkCompleted(completedAt);

        refund.Status.Should().Be(RefundStatus.Completed);
        refund.ResolvedAt.Should().Be(completedAt);
    }

    [Fact]
    public void MarkFailed_FromRequested_SetsFailedWithTimestamp()
    {
        var refund = Request();
        var failedAt = RequestedAt.AddMinutes(3);

        refund.MarkFailed(failedAt);

        refund.Status.Should().Be(RefundStatus.Failed);
        refund.ResolvedAt.Should().Be(failedAt);
    }

    [Fact]
    public void MarkCompleted_WhenAlreadyCompleted_IsNoOp()
    {
        var refund = Request();
        var completedAt = RequestedAt.AddMinutes(3);
        refund.MarkCompleted(completedAt);

        refund.MarkCompleted(RequestedAt.AddMinutes(9));

        refund.Status.Should().Be(RefundStatus.Completed);
        refund.ResolvedAt.Should().Be(completedAt);
    }

    [Fact]
    public void MarkCompleted_WhenFailed_Throws()
    {
        var refund = Request();
        refund.MarkFailed(RequestedAt.AddMinutes(3));

        var act = () => refund.MarkCompleted(RequestedAt.AddMinutes(4));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkFailed_WhenCompleted_Throws()
    {
        var refund = Request();
        refund.MarkCompleted(RequestedAt.AddMinutes(3));

        var act = () => refund.MarkFailed(RequestedAt.AddMinutes(4));

        act.Should().Throw<InvalidOperationException>();
    }
}
