using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Common;
using OfrenCollect.Domain.Mandates;
using OfrenCollect.Domain.Payments;

namespace OfrenCollect.Application.Mandates.ReconcileMandateDebit;

/// <summary>
/// Idempotent: acts only on a still-<see cref="MandateDebitStatus.Pending"/> debit and only on a
/// terminal Monnify status, so a repeated call (poll or retry) never double-applies a payment (§10).
/// The invoice mutation and the debit's status change commit together (one SaveChanges), so a failed
/// save leaves the debit pending for a safe retry.
/// </summary>
public sealed class ReconcileMandateDebitCommandHandler
    : IRequestHandler<ReconcileMandateDebitCommand, MandateDebitStatusResult>
{
    private const string PaidStatus = "PAID";
    private const string FailedStatus = "FAILED";

    private readonly IMandateDebitRepository _debits;
    private readonly IInvoiceRepository _invoices;
    private readonly IPaymentEventRepository _payments;
    private readonly IMonnifyMandateClient _monnify;
    private readonly IReconciliationNotifier _notifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public ReconcileMandateDebitCommandHandler(
        IMandateDebitRepository debits,
        IInvoiceRepository invoices,
        IPaymentEventRepository payments,
        IMonnifyMandateClient monnify,
        IReconciliationNotifier notifier,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        TimeProvider clock)
    {
        _debits = debits;
        _invoices = invoices;
        _payments = payments;
        _monnify = monnify;
        _notifier = notifier;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<MandateDebitStatusResult> Handle(
        ReconcileMandateDebitCommand command, CancellationToken cancellationToken)
    {
        _ = _tenantContext.RequireTenantId();

        var debit = await _debits.GetByPaymentReferenceAsync(command.PaymentReference, cancellationToken)
            ?? throw new NotFoundException("Debit not found.");

        if (debit.Status != MandateDebitStatus.Pending)
        {
            return new MandateDebitStatusResult(debit.PaymentReference, debit.Status.ToString());
        }

        var status = await _monnify.GetDebitStatusAsync(command.PaymentReference, cancellationToken);
        var now = _clock.GetUtcNow();

        if (status.Equals(PaidStatus, StringComparison.OrdinalIgnoreCase))
        {
            var invoice = await _invoices.GetByIdAsync(debit.InvoiceId, cancellationToken)
                ?? throw new NotFoundException("Invoice not found.");

            invoice.ApplyPayment(debit.Amount);
            debit.MarkPaid(now);

            // Record the inflow so the debit shows in the transactions view too, not just the
            // invoice. The transaction reference is the idempotency key.
            if (!await _payments.ExistsByReferenceAsync(debit.TransactionReference, cancellationToken))
            {
                _payments.Add(PaymentEvent.RecordMatched(
                    debit.TenantId, debit.TransactionReference, debit.MandateReference, debit.Amount, now, invoice.Id));
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _notifier.PaymentReconciledAsync(
                debit.TenantId, invoice.SubscriptionId, invoice.Id, invoice.Status, invoice.AmountPaid, cancellationToken);
        }
        else if (status.Equals(FailedStatus, StringComparison.OrdinalIgnoreCase))
        {
            debit.MarkFailed(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new MandateDebitStatusResult(debit.PaymentReference, debit.Status.ToString());
    }
}
