using OfrenCollect.Domain.Invoices;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Application.Abstractions;

/// <summary>
/// Pushes reconciliation events to the real-time layer (FR-5.1). The application depends on
/// this abstraction, not on SignalR, so domain/application code stays free of transport
/// concerns. Implementations broadcast to per-tenant groups only.
/// </summary>
public interface IReconciliationNotifier
{
    /// <summary>A verified payment updated an invoice for a tenant.</summary>
    Task PaymentReconciledAsync(
        Guid tenantId,
        Guid subscriptionId,
        Guid invoiceId,
        InvoiceStatus status,
        Money amountPaid,
        CancellationToken cancellationToken);

    /// <summary>An inflow matched no subscription and was surfaced for review.</summary>
    Task UnmatchedPaymentAsync(
        string transactionReference,
        Money amount,
        string accountNumber,
        CancellationToken cancellationToken);

    /// <summary>A subscription passed its due date unpaid.</summary>
    Task SubscriptionOverdueAsync(Guid tenantId, Guid subscriptionId, CancellationToken cancellationToken);
}
