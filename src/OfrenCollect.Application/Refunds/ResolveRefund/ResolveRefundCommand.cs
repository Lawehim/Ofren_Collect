using MediatR;

namespace OfrenCollect.Application.Refunds.ResolveRefund;

/// <summary>
/// Resolves a refund to its terminal state after a Monnify refund webhook (FR-11.4). The webhook is
/// only a trigger — the handler re-verifies the status with Monnify. Raised by the inbox drainer,
/// never by a client, so it carries no ambient tenant.
/// </summary>
public sealed record ResolveRefundCommand(string RefundReference) : IRequest;
