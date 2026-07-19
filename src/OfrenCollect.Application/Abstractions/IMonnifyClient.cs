namespace OfrenCollect.Application.Abstractions;

/// <summary>
/// The single boundary for all outbound Monnify calls (NFR-4.2). Keeping it an interface
/// lets the reconciliation engine be tested without HTTP and confines any field/endpoint
/// corrections to one implementation.
/// </summary>
public interface IMonnifyClient
{
    /// <summary>
    /// Provisions a dedicated reserved account for a subscription (FR-2.3). The returned
    /// account number is the identity money is later reconciled by.
    /// </summary>
    Task<ReservedAccount> CreateReservedAccountAsync(
        CreateReservedAccountRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Independently confirms a transaction with Monnify by its reference, returning the
    /// authoritative amount, destination account, and paid-at time. Reconciliation trusts
    /// this, not the webhook body.
    /// </summary>
    Task<VerifiedTransaction> VerifyTransactionAsync(string transactionReference, CancellationToken cancellationToken);
}
