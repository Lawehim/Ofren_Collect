using MediatR;

namespace OfrenCollect.Application.Mandates.ResolveMandateStatus;

/// <summary>
/// Resolves a mandate's status after its `MANDATE_UPDATE` webhook (FR-9.2). The webhook is a trigger
/// only — the handler re-verifies the status with Monnify. Raised by the inbox drainer, so it carries
/// no ambient tenant.
/// </summary>
public sealed record ResolveMandateStatusCommand(string MandateReference) : IRequest;
