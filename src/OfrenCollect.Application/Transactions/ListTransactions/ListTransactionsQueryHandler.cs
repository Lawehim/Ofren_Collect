using MediatR;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Transactions;

namespace OfrenCollect.Application.Transactions.ListTransactions;

public sealed class ListTransactionsQueryHandler
    : IRequestHandler<ListTransactionsQuery, IReadOnlyList<TransactionRow>>
{
    private readonly ITransactionReader _reader;

    public ListTransactionsQueryHandler(ITransactionReader reader) => _reader = reader;

    public Task<IReadOnlyList<TransactionRow>> Handle(
        ListTransactionsQuery query, CancellationToken cancellationToken) =>
        _reader.ListAsync(cancellationToken);
}
