using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Payments;

namespace OfrenCollect.Repository.Persistence.Repositories;

/// <inheritdoc />
public sealed class PaymentEventRepository : IPaymentEventRepository
{
    private readonly OfrenDbContext _db;

    public PaymentEventRepository(OfrenDbContext db) => _db = db;

    public Task<bool> ExistsByReferenceAsync(string transactionReference, CancellationToken cancellationToken) =>
        _db.PaymentEvents
            .AsNoTracking()
            .AnyAsync(p => p.MonnifyTransactionReference == transactionReference, cancellationToken);

    public void Add(PaymentEvent paymentEvent) => _db.PaymentEvents.Add(paymentEvent);
}
