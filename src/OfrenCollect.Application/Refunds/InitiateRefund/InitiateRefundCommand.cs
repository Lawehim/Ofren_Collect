using MediatR;

namespace OfrenCollect.Application.Refunds.InitiateRefund;

/// <summary>
/// Refunds money to a customer against an original transaction (FR-11.1). <see cref="RefundReference"/>
/// is a caller-supplied unique key so a retried or double-clicked request is idempotent (FR-11.3).
/// </summary>
public sealed record InitiateRefundCommand(
    string OriginalTransactionReference,
    decimal Amount,
    string Reason,
    string RefundReference) : IRequest<RefundResult>;
