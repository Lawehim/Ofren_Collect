using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Transactions;
using OfrenCollect.Domain.Refunds;

namespace OfrenCollect.Repository.Persistence;

/// <summary>
/// Lists the current tenant's reconciled inflows for the transactions view. PaymentEvents sit
/// outside the global filter (unmatched inflows have no tenant), so they are scoped explicitly here;
/// subscriptions, customers, and refunds are tenant-scoped by the global filter. Data is loaded in a
/// few round trips and projected in memory — no N+1.
/// </summary>
public sealed class TransactionReader : ITransactionReader
{
    private readonly OfrenDbContext _db;
    private readonly ITenantContext _tenantContext;

    public TransactionReader(OfrenDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<TransactionRow>> ListAsync(CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.CurrentTenantId;

        var payments = await _db.PaymentEvents
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.MatchedInvoiceId != null)
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync(cancellationToken);

        if (payments.Count == 0)
        {
            return [];
        }

        // Resolve the customer via the matched invoice, so both reserved-account inflows and
        // mandate debits (whose "account" is the mandate reference, not a real account) resolve.
        var invoicesById = await _db.Invoices.AsNoTracking().ToDictionaryAsync(i => i.Id, cancellationToken);
        var subscriptionsById = await _db.Subscriptions.AsNoTracking().ToDictionaryAsync(s => s.Id, cancellationToken);
        var customers = await _db.Customers.AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);

        var references = payments.Select(p => p.MonnifyTransactionReference).ToList();
        var refundAmounts = await _db.Refunds
            .AsNoTracking()
            .Where(r => references.Contains(r.OriginalTransactionReference) && r.Status != RefundStatus.Failed)
            .Select(r => new { r.OriginalTransactionReference, r.Amount.Amount })
            .ToListAsync(cancellationToken);
        var refundedByReference = refundAmounts
            .GroupBy(r => r.OriginalTransactionReference)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        return payments
            .Select(payment =>
            {
                var customerName = string.Empty;
                if (payment.MatchedInvoiceId is { } invoiceId
                    && invoicesById.TryGetValue(invoiceId, out var invoice)
                    && subscriptionsById.TryGetValue(invoice.SubscriptionId, out var subscription)
                    && customers.TryGetValue(subscription.CustomerId, out var customer))
                {
                    customerName = customer.Name;
                }

                var refunded = refundedByReference.GetValueOrDefault(payment.MonnifyTransactionReference, 0m);
                var amount = payment.Amount.Amount;

                return new TransactionRow(
                    payment.MonnifyTransactionReference,
                    customerName,
                    amount,
                    refunded,
                    amount - refunded,
                    payment.ReservedAccountNumber,
                    payment.PaidAt);
            })
            .ToList();
    }
}
