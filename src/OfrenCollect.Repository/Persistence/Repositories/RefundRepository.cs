using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Refunds;

namespace OfrenCollect.Repository.Persistence.Repositories;

/// <inheritdoc />
public sealed class RefundRepository : IRefundRepository
{
    private readonly OfrenDbContext _db;

    public RefundRepository(OfrenDbContext db) => _db = db;

    public void Add(Refund refund) => _db.Refunds.Add(refund);

    // Tenant-scoped by the global query filter (Refund is tenant-owned).
    public Task<Refund?> GetByReferenceAsync(string refundReference, CancellationToken cancellationToken) =>
        _db.Refunds
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RefundReference == refundReference, cancellationToken);

    // Webhook path: no ambient tenant, so bypass the global filter; tracked so the status change is
    // saved by the drainer's unit of work. Refund references are globally unique.
    public Task<Refund?> GetForResolutionAsync(string refundReference, CancellationToken cancellationToken) =>
        _db.Refunds
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.RefundReference == refundReference, cancellationToken);

    public async Task<decimal> TotalRefundedForTransactionAsync(
        string originalTransactionReference, CancellationToken cancellationToken)
    {
        // Requested and Completed refunds both hold refundable headroom; only Failed ones free it.
        var amounts = await _db.Refunds
            .AsNoTracking()
            .Where(r => r.OriginalTransactionReference == originalTransactionReference
                && r.Status != RefundStatus.Failed)
            .Select(r => r.Amount.Amount)
            .ToListAsync(cancellationToken);

        return amounts.Sum();
    }
}
