using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Payments;

namespace OfrenCollect.Repository.Persistence.Repositories;

/// <inheritdoc />
public sealed class PaymentEventRepository : IPaymentEventRepository
{
    private readonly OfrenDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PaymentEventRepository(OfrenDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public Task<bool> ExistsByReferenceAsync(string transactionReference, CancellationToken cancellationToken) =>
        _db.PaymentEvents
            .AsNoTracking()
            .AnyAsync(p => p.MonnifyTransactionReference == transactionReference, cancellationToken);

    public Task<PaymentEvent?> GetMatchedByReferenceAsync(
        string transactionReference, CancellationToken cancellationToken)
    {
        // PaymentEvent is deliberately outside the global tenant filter (unmatched inflows have no
        // tenant), so scope explicitly to the current tenant here. A tenant-less or other-tenant
        // payment therefore never matches — that is what stops a refund the caller doesn't own.
        var tenantId = _tenantContext.CurrentTenantId;
        return _db.PaymentEvents
            .AsNoTracking()
            .Where(p => p.MonnifyTransactionReference == transactionReference
                && p.TenantId == tenantId
                && p.MatchedInvoiceId != null)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(PaymentEvent paymentEvent) => _db.PaymentEvents.Add(paymentEvent);
}
