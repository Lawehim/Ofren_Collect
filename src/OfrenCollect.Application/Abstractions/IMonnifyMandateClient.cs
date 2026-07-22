using OfrenCollect.SharedKernel;

namespace OfrenCollect.Application.Abstractions;

/// <summary>
/// The boundary for Monnify direct-debit (mandate) calls (FR-9), kept separate from the other
/// Monnify interfaces so each stays small and focused (§5-I). The concrete implementation confines
/// the exact endpoints and payloads to one file (§2.3, §14).
/// </summary>
public interface IMonnifyMandateClient
{
    /// <summary>
    /// Creates a mandate. Monnify returns a mandate code and an authorization link the customer must
    /// use to authorise recurring debits; the mandate is not debitable until it becomes Active.
    /// </summary>
    Task<MandateCreationResult> CreateMandateAsync(MandateCreationRequest request, CancellationToken cancellationToken);

    /// <summary>The mandate's current status (used to confirm authorisation without a webhook).</summary>
    Task<MonnifyMandateStatus> GetMandateStatusAsync(string mandateReference, CancellationToken cancellationToken);

    /// <summary>
    /// The customer authorization link for a mandate — returned by get-mandate-status, not by create
    /// (whose redirectUrl is a merchant page). Send this to the customer to authorise recurring debits.
    /// </summary>
    Task<string> GetMandateAuthorizationLinkAsync(string mandateReference, CancellationToken cancellationToken);

    /// <summary>
    /// Debits an active mandate. The returned transaction reference reconciles through the existing
    /// verify/transaction path, exactly like a reserved-account inflow.
    /// </summary>
    Task<MandateDebitResult> DebitMandateAsync(MandateDebitRequest request, CancellationToken cancellationToken);

    /// <summary>The status of a debit (by its payment reference), for reconciling it to the invoice.</summary>
    Task<string> GetDebitStatusAsync(string paymentReference, CancellationToken cancellationToken);

    /// <summary>Cancels/deactivates a mandate so no further debits can be made.</summary>
    Task CancelMandateAsync(string mandateCode, CancellationToken cancellationToken);
}

/// <summary>What Monnify needs to create a mandate (fields confirmed against the docs).</summary>
public sealed record MandateCreationRequest(
    string MandateReference,
    Money Amount,
    string Description,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhoneNumber,
    string CustomerAddress,
    string CustomerAccountNumber,
    string CustomerAccountBankCode,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate);

/// <summary>The result of creating a mandate: Monnify's code, the customer authorization link, and status.</summary>
public sealed record MandateCreationResult(string MandateCode, string AuthorizationLink, MonnifyMandateStatus Status);

/// <summary>What Monnify needs to debit an active mandate.</summary>
public sealed record MandateDebitRequest(
    string PaymentReference,
    string MandateCode,
    Money Amount,
    string Narration,
    string CustomerEmail);

/// <summary>The result of a debit: the transaction reference to reconcile, and its initial status.</summary>
public sealed record MandateDebitResult(string TransactionReference, string TransactionStatus);

/// <summary>Monnify's mandate status, normalised.</summary>
public enum MonnifyMandateStatus
{
    Unknown = 0,
    Initiated,
    Active,
    Failed,
    Cancelled,
    Expired
}
