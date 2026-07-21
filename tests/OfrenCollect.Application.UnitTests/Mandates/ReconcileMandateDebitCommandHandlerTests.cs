using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Mandates.ReconcileMandateDebit;
using OfrenCollect.Domain.Invoices;
using OfrenCollect.Domain.Mandates;
using OfrenCollect.Domain.Payments;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Application.UnitTests.Mandates;

public class ReconcileMandateDebitCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubscriptionId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
    private const string PaymentRef = "OFREN-DBT-1";

    private readonly IMandateDebitRepository _debits = Substitute.For<IMandateDebitRepository>();
    private readonly IInvoiceRepository _invoices = Substitute.For<IInvoiceRepository>();
    private readonly IPaymentEventRepository _payments = Substitute.For<IPaymentEventRepository>();
    private readonly IMonnifyMandateClient _monnify = Substitute.For<IMonnifyMandateClient>();
    private readonly IReconciliationNotifier _notifier = Substitute.For<IReconciliationNotifier>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private readonly Invoice _invoice = Invoice.Create(TenantId, SubscriptionId, Money.Of(5000m), Now, Now.AddDays(7));

    public ReconcileMandateDebitCommandHandlerTests()
    {
        _tenantContext.CurrentTenantId.Returns(TenantId);
        _invoices.GetByIdAsync(_invoice.Id, Arg.Any<CancellationToken>()).Returns(_invoice);
    }

    private ReconcileMandateDebitCommandHandler CreateHandler() =>
        new(_debits, _invoices, _payments, _monnify, _notifier, _unitOfWork, _tenantContext, new FixedClock(Now));

    private MandateDebit PendingDebit() =>
        MandateDebit.Initiate(TenantId, "OFREN-MND-1", _invoice.Id, PaymentRef, "MNFY|TX-1", Money.Of(5000m), Now);

    private void GivenDebit(MandateDebit debit) =>
        _debits.GetByPaymentReferenceAsync(PaymentRef, Arg.Any<CancellationToken>()).Returns(debit);

    [Fact]
    public async Task Handle_WhenPaid_AppliesToInvoice_MarksDebitPaid_AndNotifies()
    {
        var debit = PendingDebit();
        GivenDebit(debit);
        _monnify.GetDebitStatusAsync(PaymentRef, Arg.Any<CancellationToken>()).Returns("PAID");

        var result = await CreateHandler().Handle(new ReconcileMandateDebitCommand(PaymentRef), CancellationToken.None);

        result.Status.Should().Be(nameof(MandateDebitStatus.Paid));
        _invoice.Status.Should().Be(InvoiceStatus.Paid);
        _invoice.AmountPaid.Should().Be(Money.Of(5000m));
        debit.Status.Should().Be(MandateDebitStatus.Paid);
        _payments.Received(1).Add(Arg.Is<PaymentEvent>(p =>
            p != null && p.MonnifyTransactionReference == "MNFY|TX-1" && p.MatchedInvoiceId == _invoice.Id));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifier.Received(1).PaymentReconciledAsync(
            TenantId, SubscriptionId, _invoice.Id, InvoiceStatus.Paid, Arg.Any<Money>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenStillPending_LeavesEverythingUnchanged()
    {
        var debit = PendingDebit();
        GivenDebit(debit);
        _monnify.GetDebitStatusAsync(PaymentRef, Arg.Any<CancellationToken>()).Returns("PENDING");

        await CreateHandler().Handle(new ReconcileMandateDebitCommand(PaymentRef), CancellationToken.None);

        debit.Status.Should().Be(MandateDebitStatus.Pending);
        _invoice.AmountPaid.Should().Be(Money.Zero());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAlreadyPaid_IsNoOp_AndDoesNotReCheckMonnify()
    {
        var debit = PendingDebit();
        debit.MarkPaid(Now);
        GivenDebit(debit);

        await CreateHandler().Handle(new ReconcileMandateDebitCommand(PaymentRef), CancellationToken.None);

        await _monnify.DidNotReceive().GetDebitStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _invoice.AmountPaid.Should().Be(Money.Zero());
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
