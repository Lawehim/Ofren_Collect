using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Payments;

namespace OfrenCollect.Application.Reconciliation.HandleTransactionNotification;

/// <summary>
/// The reconciliation engine (FR-3.x, FR-4.x). Ordering matters: idempotency first, then
/// server-side verification, then resolve-by-reserved-account, apply, persist, and only
/// after a durable commit does it push the result to the dashboard.
/// </summary>
public sealed class HandleTransactionNotificationHandler
    : IRequestHandler<HandleTransactionNotificationCommand>
{
    private readonly IMonnifyClient _monnify;
    private readonly IPaymentEventRepository _payments;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IInvoiceRepository _invoices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReconciliationNotifier _notifier;

    public HandleTransactionNotificationHandler(
        IMonnifyClient monnify,
        IPaymentEventRepository payments,
        ISubscriptionRepository subscriptions,
        IInvoiceRepository invoices,
        IUnitOfWork unitOfWork,
        IReconciliationNotifier notifier)
    {
        _monnify = monnify;
        _payments = payments;
        _subscriptions = subscriptions;
        _invoices = invoices;
        _unitOfWork = unitOfWork;
        _notifier = notifier;
    }

    public async Task Handle(HandleTransactionNotificationCommand command, CancellationToken cancellationToken)
    {
        var reference = command.TransactionReference;

        // 1. Idempotency: a reference we have already processed does nothing (FR-3.6).
        if (await _payments.ExistsByReferenceAsync(reference, cancellationToken))
        {
            return;
        }

        // 2. Never trust the webhook body's figures — confirm amount/status with Monnify
        //    (FR-3.4, NFR-1.5).
        var verified = await _monnify.VerifyTransactionAsync(reference, cancellationToken);
        if (!verified.IsSuccessful)
        {
            return;
        }

        var reservedAccountNumber = command.DestinationAccountNumber;

        // 3. Resolve the owning subscription from the reserved account paid into (FR-4.1, §11.3).
        //    The account comes from the (signature-authenticated) webhook, not the verify
        //    response, whose account field is the payer's and may be masked.
        var subscription = await _subscriptions.FindByReservedAccountNumberAsync(
            reservedAccountNumber, cancellationToken);
        if (subscription is null)
        {
            await RecordUnmatchedAsync(reference, reservedAccountNumber, verified, cancellationToken);
            return;
        }

        // 4. Apply to the current open invoice; if there is none, surface for review (TC-4.12).
        var invoice = await _invoices.GetOpenInvoiceForSubscriptionAsync(subscription.Id, cancellationToken);
        if (invoice is null)
        {
            await RecordUnmatchedAsync(reference, reservedAccountNumber, verified, cancellationToken);
            return;
        }

        invoice.ApplyPayment(verified.Amount);

        var payment = PaymentEvent.RecordMatched(
            subscription.TenantId,
            reference,
            reservedAccountNumber,
            verified.Amount,
            verified.PaidAt,
            invoice.Id);
        _payments.Add(payment);

        // 5. Commit durably before pushing, so the dashboard only ever sees committed truth.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _notifier.PaymentReconciledAsync(
            subscription.TenantId, subscription.Id, invoice.Id, invoice.Status, invoice.AmountPaid, cancellationToken);
    }

    private async Task RecordUnmatchedAsync(
        string reference, string reservedAccountNumber, VerifiedTransaction verified, CancellationToken cancellationToken)
    {
        var payment = PaymentEvent.RecordUnmatched(
            reference, reservedAccountNumber, verified.Amount, verified.PaidAt);
        _payments.Add(payment);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _notifier.UnmatchedPaymentAsync(
            reference, verified.Amount, reservedAccountNumber, cancellationToken);
    }
}
