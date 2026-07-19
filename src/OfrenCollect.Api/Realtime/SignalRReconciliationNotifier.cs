using Microsoft.AspNetCore.SignalR;
using OfrenCollect.Api.Hubs;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Domain.Invoices;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Api.Realtime;

/// <summary>
/// Pushes reconciliation events over SignalR to per-tenant groups. Broadcasts happen only after
/// the caller has durably persisted state, so clients only ever see committed truth.
/// </summary>
public sealed class SignalRReconciliationNotifier : IReconciliationNotifier
{
    private readonly IHubContext<NotificationsHub> _hub;

    public SignalRReconciliationNotifier(IHubContext<NotificationsHub> hub) => _hub = hub;

    public Task PaymentReconciledAsync(
        Guid tenantId,
        Guid subscriptionId,
        Guid invoiceId,
        InvoiceStatus status,
        Money amountPaid,
        CancellationToken cancellationToken) =>
        _hub.Clients.Group(NotificationsHub.TenantGroup(tenantId)).SendAsync(
            "PaymentReconciled",
            new { subscriptionId, invoiceId, status = status.ToString(), amountPaid = amountPaid.Amount },
            cancellationToken);

    public Task SubscriptionOverdueAsync(Guid tenantId, Guid subscriptionId, CancellationToken cancellationToken) =>
        _hub.Clients.Group(NotificationsHub.TenantGroup(tenantId)).SendAsync(
            "SubscriptionOverdue", new { subscriptionId }, cancellationToken);

    public Task UnmatchedPaymentAsync(
        string transactionReference, Money amount, string accountNumber, CancellationToken cancellationToken) =>
        // Unmatched inflows are tenant-less, so there is no per-tenant group to push to; they
        // surface via the dashboard's unmatched count on the next read.
        Task.CompletedTask;
}
