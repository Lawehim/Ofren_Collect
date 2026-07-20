using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Refunds.ResolveRefund;
using OfrenCollect.Domain.Refunds;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Application.UnitTests.Refunds;

public class ResolveRefundCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 13, 0, 0, TimeSpan.Zero);
    private const string RefundRef = "OFREN-RF-1";

    private readonly IRefundRepository _refunds = Substitute.For<IRefundRepository>();
    private readonly IMonnifyRefundClient _monnify = Substitute.For<IMonnifyRefundClient>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private ResolveRefundCommandHandler CreateHandler() => new(_refunds, _monnify, _unitOfWork, new FixedClock(Now));

    private static Refund RequestedRefund() =>
        Refund.Request(TenantId, "MNFY-ORIG-1", RefundRef, Money.Of(5000m), "Overpaid", Money.Of(25000m), Now);

    private void GivenRefund(Refund refund) =>
        _refunds.GetForResolutionAsync(RefundRef, Arg.Any<CancellationToken>()).Returns(refund);

    private void GivenMonnifyStatus(MonnifyRefundStatus status) =>
        _monnify.GetRefundStatusAsync(RefundRef, Arg.Any<CancellationToken>()).Returns(status);

    [Fact]
    public async Task Handle_WhenMonnifyConfirmsCompleted_MarksRefundCompleted_AndSaves()
    {
        var refund = RequestedRefund();
        GivenRefund(refund);
        GivenMonnifyStatus(MonnifyRefundStatus.Completed);

        await CreateHandler().Handle(new ResolveRefundCommand(RefundRef), CancellationToken.None);

        refund.Status.Should().Be(RefundStatus.Completed);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMonnifyReportsFailed_MarksRefundFailed()
    {
        var refund = RequestedRefund();
        GivenRefund(refund);
        GivenMonnifyStatus(MonnifyRefundStatus.Failed);

        await CreateHandler().Handle(new ResolveRefundCommand(RefundRef), CancellationToken.None);

        refund.Status.Should().Be(RefundStatus.Failed);
    }

    [Fact]
    public async Task Handle_WhenMonnifyStillPending_LeavesRefundRequested_WithoutSaving()
    {
        var refund = RequestedRefund();
        GivenRefund(refund);
        GivenMonnifyStatus(MonnifyRefundStatus.Pending);

        await CreateHandler().Handle(new ResolveRefundCommand(RefundRef), CancellationToken.None);

        refund.Status.Should().Be(RefundStatus.Requested);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRefundUnknown_DoesNotCallMonnify()
    {
        _refunds.GetForResolutionAsync(RefundRef, Arg.Any<CancellationToken>()).Returns((Refund?)null);

        await CreateHandler().Handle(new ResolveRefundCommand(RefundRef), CancellationToken.None);

        await _monnify.DidNotReceive().GetRefundStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAlreadyResolved_DoesNotReVerify_NorThrow()
    {
        var refund = RequestedRefund();
        refund.MarkCompleted(Now);
        GivenRefund(refund);

        var act = () => CreateHandler().Handle(new ResolveRefundCommand(RefundRef), CancellationToken.None);

        await act.Should().NotThrowAsync();
        await _monnify.DidNotReceive().GetRefundStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        refund.Status.Should().Be(RefundStatus.Completed);
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
