using OfrenCollect.SharedKernel;

namespace OfrenCollect.Application.Abstractions;

/// <summary>
/// The boundary for Monnify refund calls (FR-11), separate from <see cref="IMonnifyClient"/> so
/// each interface stays small and focused (§5-I). The concrete implementation confines the exact
/// Monnify endpoint and payload to one file, so a correction touches only it (§2.3, §14).
/// </summary>
public interface IMonnifyRefundClient
{
    /// <summary>
    /// Initiates a refund against an original transaction. Returns the refund's status as reported
    /// by Monnify; a <see cref="MonnifyRefundStatus.Pending"/> refund is later resolved by the
    /// refund webhook.
    /// </summary>
    Task<RefundInitiationResult> InitiateRefundAsync(
        RefundInitiationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Independently confirms a refund's current status with Monnify by its reference (FR-11.4). The
    /// refund webhook is only a trigger; this is the authoritative source we act on, never the
    /// webhook body (§8).
    /// </summary>
    Task<MonnifyRefundStatus> GetRefundStatusAsync(string refundReference, CancellationToken cancellationToken);
}

/// <summary>What Monnify needs to initiate a refund (fields confirmed against the dev portal).</summary>
public sealed record RefundInitiationRequest(
    string OriginalTransactionReference,
    string RefundReference,
    Money Amount,
    string Reason,
    string CustomerNote);

/// <summary>The outcome of initiating a refund.</summary>
public sealed record RefundInitiationResult(MonnifyRefundStatus Status);

/// <summary>Monnify's refund status, normalised (PENDING / COMPLETED / FAILED).</summary>
public enum MonnifyRefundStatus
{
    Pending = 0,
    Completed,
    Failed
}
