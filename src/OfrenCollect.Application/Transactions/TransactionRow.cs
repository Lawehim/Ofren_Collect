namespace OfrenCollect.Application.Transactions;

/// <summary>
/// One reconciled inflow for the tenant's transactions view — enough to identify it and to drive a
/// refund. <see cref="RefundableAmount"/> is the original amount less refunds already made, so the
/// UI can offer a valid refund without a second round trip.
/// </summary>
public sealed record TransactionRow(
    string TransactionReference,
    string CustomerName,
    decimal Amount,
    decimal RefundedAmount,
    decimal RefundableAmount,
    string ReservedAccountNumber,
    DateTimeOffset PaidAt);
