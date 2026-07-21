using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Mandates;

namespace OfrenCollect.Repository.Persistence.Repositories;

/// <inheritdoc />
public sealed class MandateDebitRepository : IMandateDebitRepository
{
    private readonly OfrenDbContext _db;

    public MandateDebitRepository(OfrenDbContext db) => _db = db;

    public void Add(MandateDebit debit) => _db.MandateDebits.Add(debit);

    // Tenant-scoped by the global query filter; tracked so a status change is saved.
    public Task<MandateDebit?> GetByPaymentReferenceAsync(string paymentReference, CancellationToken cancellationToken) =>
        _db.MandateDebits.FirstOrDefaultAsync(d => d.PaymentReference == paymentReference, cancellationToken);

    // Tenant-scoped; excludes failed debits so a failed attempt can be retried.
    public Task<bool> HasActiveDebitForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        _db.MandateDebits
            .AsNoTracking()
            .AnyAsync(d => d.InvoiceId == invoiceId && d.Status != MandateDebitStatus.Failed, cancellationToken);

    // Background path: no ambient tenant, so bypass the global filter; tracked for resolution.
    public async Task<IReadOnlyList<MandateDebit>> GetPendingAsync(int limit, CancellationToken cancellationToken) =>
        await _db.MandateDebits
            .IgnoreQueryFilters()
            .Where(d => d.Status == MandateDebitStatus.Pending)
            .OrderBy(d => d.InitiatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
