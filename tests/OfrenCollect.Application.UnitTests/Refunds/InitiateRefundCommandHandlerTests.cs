using FluentAssertions;
using FluentValidation;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Common;
using OfrenCollect.Application.Refunds.InitiateRefund;
using OfrenCollect.Domain.Payments;
using OfrenCollect.Domain.Refunds;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Application.UnitTests.Refunds;

public class InitiateRefundCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
    private const string OriginalRef = "MNFY-ORIG-1";
    private const string RefundRef = "OFREN-RFND-1";

    private readonly IRefundRepository _refunds = Substitute.For<IRefundRepository>();
    private readonly IPaymentEventRepository _payments = Substitute.For<IPaymentEventRepository>();
    private readonly IMonnifyRefundClient _monnify = Substitute.For<IMonnifyRefundClient>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    public InitiateRefundCommandHandlerTests()
    {
        _tenantContext.CurrentTenantId.Returns(TenantId);
        _monnify.InitiateRefundAsync(Arg.Any<RefundInitiationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RefundInitiationResult(MonnifyRefundStatus.Pending));
    }

    private InitiateRefundCommandHandler CreateHandler() =>
        new(_refunds, _payments, _monnify, _unitOfWork, _tenantContext, new FixedClock(Now));

    private void GivenOriginalPayment(decimal amount = 25000m) =>
        _payments.GetMatchedByReferenceAsync(OriginalRef, Arg.Any<CancellationToken>())
            .Returns(PaymentEvent.RecordMatched(TenantId, OriginalRef, "7080000001", Money.Of(amount), Now, Guid.NewGuid()));

    private static InitiateRefundCommand Command(decimal amount = 5000m) =>
        new(OriginalRef, amount, "Overpaid July invoice", RefundRef);

    [Fact]
    public async Task Handle_ValidPartialRefund_PersistsRequestedRefund_AndReturnsIt()
    {
        GivenOriginalPayment(amount: 25000m);
        _refunds.TotalRefundedForTransactionAsync(OriginalRef, Arg.Any<CancellationToken>()).Returns(0m);

        var result = await CreateHandler().Handle(Command(amount: 5000m), CancellationToken.None);

        result.Amount.Should().Be(5000m);
        result.Status.Should().Be(nameof(RefundStatus.Requested));
        _refunds.Received(1).Add(Arg.Is<Refund>(r =>
            r != null && r.TenantId == TenantId && r.RefundReference == RefundRef && r.Status == RefundStatus.Requested));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMonnifyReportsCompleted_MarksRefundCompleted()
    {
        GivenOriginalPayment();
        _refunds.TotalRefundedForTransactionAsync(OriginalRef, Arg.Any<CancellationToken>()).Returns(0m);
        _monnify.InitiateRefundAsync(Arg.Any<RefundInitiationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RefundInitiationResult(MonnifyRefundStatus.Completed));

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.Status.Should().Be(nameof(RefundStatus.Completed));
    }

    [Fact]
    public async Task Handle_WhenOriginalTransactionNotFound_Throws_AndDoesNotCallMonnify()
    {
        _payments.GetMatchedByReferenceAsync(OriginalRef, Arg.Any<CancellationToken>()).Returns((PaymentEvent?)null);

        var act = () => CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        await _monnify.DidNotReceive().InitiateRefundAsync(Arg.Any<RefundInitiationRequest>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAmountExceedsRemainingRefundable_Throws_AndDoesNotCallMonnify()
    {
        GivenOriginalPayment(amount: 25000m);
        _refunds.TotalRefundedForTransactionAsync(OriginalRef, Arg.Any<CancellationToken>()).Returns(22000m);

        var act = () => CreateHandler().Handle(Command(amount: 5000m), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await _monnify.DidNotReceive().InitiateRefundAsync(Arg.Any<RefundInitiationRequest>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRefundReferenceAlreadyExists_ReturnsExisting_Idempotently()
    {
        var existing = Refund.Request(
            TenantId, OriginalRef, RefundRef, Money.Of(5000m), "Overpaid", Money.Of(25000m), Now);
        _refunds.GetByReferenceAsync(RefundRef, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.RefundReference.Should().Be(RefundRef);
        await _payments.DidNotReceive().GetMatchedByReferenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _monnify.DidNotReceive().InitiateRefundAsync(Arg.Any<RefundInitiationRequest>(), Arg.Any<CancellationToken>());
        _refunds.DidNotReceive().Add(Arg.Any<Refund>());
    }

    [Fact]
    public async Task Handle_WhenMonnifyThrows_DoesNotPersistRefund()
    {
        GivenOriginalPayment();
        _refunds.TotalRefundedForTransactionAsync(OriginalRef, Arg.Any<CancellationToken>()).Returns(0m);
        _monnify.InitiateRefundAsync(Arg.Any<RefundInitiationRequest>(), Arg.Any<CancellationToken>())
            .Returns<RefundInitiationResult>(_ => throw new InvalidOperationException("provider down"));

        var act = () => CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
