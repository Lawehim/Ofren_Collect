namespace OfrenCollect.Domain.Refunds;

/// <summary>
/// The lifecycle of a refund (FR-11.2). A refund is <see cref="Requested"/> once initiated with
/// Monnify, then resolves to <see cref="Completed"/> or <see cref="Failed"/> when the refund
/// webhook is verified. Maps to Monnify's PENDING / COMPLETED / FAILED.
/// </summary>
public enum RefundStatus
{
    Requested = 0,
    Completed,
    Failed
}
