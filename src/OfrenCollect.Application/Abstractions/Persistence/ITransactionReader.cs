using OfrenCollect.Application.Transactions;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Reads the current tenant's reconciled transactions for display (FR-5.x, refund UI).</summary>
public interface ITransactionReader
{
    Task<IReadOnlyList<TransactionRow>> ListAsync(CancellationToken cancellationToken);
}
