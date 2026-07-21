using MediatR;

namespace OfrenCollect.Application.Mandates.CancelMandate;

/// <summary>Cancels a mandate with Monnify and marks it revoked so no further debits are made (FR-9.6).</summary>
public sealed record CancelMandateCommand(string MandateReference) : IRequest;
