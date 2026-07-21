using MediatR;

namespace OfrenCollect.Application.Mandates.RefreshMandateStatus;

/// <summary>
/// Re-checks a mandate's status with Monnify and applies any change (e.g. the customer has now
/// authorised it, so it becomes Active) — the polling alternative to a webhook (FR-9.2).
/// </summary>
public sealed record RefreshMandateStatusCommand(string MandateReference) : IRequest<MandateStatusResult>;

/// <summary>A mandate's reference and current status.</summary>
public sealed record MandateStatusResult(string MandateReference, string Status);
