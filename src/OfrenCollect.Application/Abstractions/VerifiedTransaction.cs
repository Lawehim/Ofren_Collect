using OfrenCollect.SharedKernel;

namespace OfrenCollect.Application.Abstractions;

/// <summary>
/// The authoritative result of independently verifying a transaction with Monnify
/// (FR-3.4). Reconciliation acts on this, never on the raw webhook body (NFR-1.5).
/// </summary>
public sealed record VerifiedTransaction(
    string TransactionReference,
    Money Amount,
    string DestinationAccountNumber,
    DateTimeOffset PaidAt,
    bool IsSuccessful);
