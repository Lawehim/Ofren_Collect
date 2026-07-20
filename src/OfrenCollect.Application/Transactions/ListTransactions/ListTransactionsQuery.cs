using MediatR;
using OfrenCollect.Application.Transactions;

namespace OfrenCollect.Application.Transactions.ListTransactions;

/// <summary>Lists the current tenant's reconciled transactions, most recent first.</summary>
public sealed record ListTransactionsQuery : IRequest<IReadOnlyList<TransactionRow>>;
