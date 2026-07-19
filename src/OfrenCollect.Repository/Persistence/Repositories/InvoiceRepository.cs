using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Invoices;

namespace OfrenCollect.Repository.Persistence.Repositories;

/// <inheritdoc />
public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly OfrenDbContext _db;

    public InvoiceRepository(OfrenDbContext db) => _db = db;

    public Task<Invoice?> GetOpenInvoiceForSubscriptionAsync(
        Guid subscriptionId, CancellationToken cancellationToken) =>
        // Tracked (not AsNoTracking): the caller mutates the invoice via ApplyPayment and saves.
        // Webhook path bypasses the global filter and scopes explicitly by subscription.
        _db.Invoices
            .IgnoreQueryFilters()
            .Where(i => i.SubscriptionId == subscriptionId &&
                        (i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.Underpaid))
            .OrderBy(i => i.DueDate)
            .FirstOrDefaultAsync(cancellationToken);
}
