using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Reconciliation.HandleTransactionNotification;
using OfrenCollect.Domain.Invoices;
using OfrenCollect.Domain.Payments;
using OfrenCollect.Domain.Subscriptions;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Application.UnitTests.Reconciliation;

public class HandleTransactionNotificationHandlerTests
{
    private const string Reference = "MNFY-7A3C91";
    private const string AccountNumber = "7080124933";
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset PaidAt = new(2026, 7, 17, 9, 13, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DueDate = new(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);

    private readonly IMonnifyClient _monnify = Substitute.For<IMonnifyClient>();
    private readonly IPaymentEventRepository _payments = Substitute.For<IPaymentEventRepository>();
    private readonly ISubscriptionRepository _subscriptions = Substitute.For<ISubscriptionRepository>();
    private readonly IInvoiceRepository _invoices = Substitute.For<IInvoiceRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IReconciliationNotifier _notifier = Substitute.For<IReconciliationNotifier>();

    private HandleTransactionNotificationHandler CreateHandler() =>
        new(_monnify, _payments, _subscriptions, _invoices, _unitOfWork, _notifier);

    private static HandleTransactionNotificationCommand Command() => new(Reference, AccountNumber);

    private void GivenVerifiedAmount(decimal amount, bool successful = true) =>
        _monnify.VerifyTransactionAsync(Reference, Arg.Any<CancellationToken>())
            .Returns(new VerifiedTransaction(Reference, Money.Of(amount), AccountNumber, PaidAt, successful));

    private static Subscription ActiveSubscription() =>
        Subscription.Enrol(TenantId, Guid.NewGuid(), Guid.NewGuid(), "OFREN-SUB-1", DueDate);

    private static Invoice OpenInvoice(decimal amountDue) =>
        Invoice.Create(TenantId, Guid.NewGuid(), Money.Of(amountDue), PeriodStart, DueDate);

    [Fact]
    public async Task Handle_WhenReferenceAlreadyProcessed_DoesNothing()
    {
        _payments.ExistsByReferenceAsync(Reference, Arg.Any<CancellationToken>()).Returns(true);

        await CreateHandler().Handle(Command(), CancellationToken.None);

        await _monnify.DidNotReceive().VerifyTransactionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _payments.DidNotReceive().Add(Arg.Any<PaymentEvent>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenVerificationUnsuccessful_RecordsNothing()
    {
        _payments.ExistsByReferenceAsync(Reference, Arg.Any<CancellationToken>()).Returns(false);
        GivenVerifiedAmount(5000m, successful: false);

        await CreateHandler().Handle(Command(), CancellationToken.None);

        _payments.DidNotReceive().Add(Arg.Any<PaymentEvent>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoSubscriptionMatches_RecordsUnmatchedAndNotifies()
    {
        _payments.ExistsByReferenceAsync(Reference, Arg.Any<CancellationToken>()).Returns(false);
        GivenVerifiedAmount(5000m);
        _subscriptions.FindByReservedAccountNumberAsync(AccountNumber, Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        await CreateHandler().Handle(Command(), CancellationToken.None);

        _payments.Received(1).Add(Arg.Is<PaymentEvent>(p => p != null && !p.IsMatched && p.TenantId == null));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifier.Received(1).UnmatchedPaymentAsync(
            Reference, Arg.Any<Money>(), AccountNumber, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExactPayment_MarksInvoicePaid_RecordsMatched_AndNotifies()
    {
        var subscription = ActiveSubscription();
        var invoice = OpenInvoice(5000m);
        GivenMatched(subscription, invoice);
        GivenVerifiedAmount(5000m);

        await CreateHandler().Handle(Command(), CancellationToken.None);

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        _payments.Received(1).Add(Arg.Is<PaymentEvent>(p =>
            p != null && p.IsMatched && p.TenantId == TenantId && p.MatchedInvoiceId == invoice.Id));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifier.Received(1).PaymentReconciledAsync(
            TenantId, subscription.Id, invoice.Id, InvoiceStatus.Paid, Arg.Any<Money>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUnderpayment_MarksInvoiceUnderpaid()
    {
        var subscription = ActiveSubscription();
        var invoice = OpenInvoice(5000m);
        GivenMatched(subscription, invoice);
        GivenVerifiedAmount(3000m);

        await CreateHandler().Handle(Command(), CancellationToken.None);

        invoice.Status.Should().Be(InvoiceStatus.Underpaid);
        await _notifier.Received(1).PaymentReconciledAsync(
            TenantId, subscription.Id, invoice.Id, InvoiceStatus.Underpaid, Arg.Any<Money>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSubscriptionFoundButNoOpenInvoice_RecordsUnmatched()
    {
        var subscription = ActiveSubscription();
        _payments.ExistsByReferenceAsync(Reference, Arg.Any<CancellationToken>()).Returns(false);
        GivenVerifiedAmount(5000m);
        _subscriptions.FindByReservedAccountNumberAsync(AccountNumber, Arg.Any<CancellationToken>())
            .Returns(subscription);
        _invoices.GetOpenInvoiceForSubscriptionAsync(subscription.Id, Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        await CreateHandler().Handle(Command(), CancellationToken.None);

        _payments.Received(1).Add(Arg.Is<PaymentEvent>(p => p != null && !p.IsMatched));
        await _notifier.Received(1).UnmatchedPaymentAsync(
            Reference, Arg.Any<Money>(), AccountNumber, Arg.Any<CancellationToken>());
    }

    private void GivenMatched(Subscription subscription, Invoice invoice)
    {
        _payments.ExistsByReferenceAsync(Reference, Arg.Any<CancellationToken>()).Returns(false);
        _subscriptions.FindByReservedAccountNumberAsync(AccountNumber, Arg.Any<CancellationToken>())
            .Returns(subscription);
        _invoices.GetOpenInvoiceForSubscriptionAsync(subscription.Id, Arg.Any<CancellationToken>())
            .Returns(invoice);
    }
}
