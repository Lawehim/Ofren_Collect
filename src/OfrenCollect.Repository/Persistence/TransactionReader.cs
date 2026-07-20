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

        var subscriptionsByAccount = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.ReservedAccountNumber != null)
            .ToDictionaryAsync(s => s.ReservedAccountNumber!, cancellationToken);
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
                if (subscriptionsByAccount.TryGetValue(payment.ReservedAccountNumber, out var subscription)
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
