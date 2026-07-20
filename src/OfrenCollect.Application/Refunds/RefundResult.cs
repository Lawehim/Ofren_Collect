using OfrenCollect.Domain.Refunds;

namespace OfrenCollect.Application.Refunds;

/// <summary>What the caller needs back after initiating a refund (§2.1 — commands return the minimum).</summary>
public sealed record RefundResult(
    Guid Id,
    string RefundReference,
    string OriginalTransactionReference,
    decimal Amount,
    string Status)
{
    public static RefundResult From(Refund refund) => new(
        refund.Id,
        refund.RefundReference,
        refund.OriginalTransactionReference,
        refund.Amount.Amount,
        refund.Status.ToString());
}
