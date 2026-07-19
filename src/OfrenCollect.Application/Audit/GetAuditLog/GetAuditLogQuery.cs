using MediatR;

namespace OfrenCollect.Application.Audit.GetAuditLog;

/// <summary>Returns the current tenant's recent audit trail (FR-8.3, Owner-only).</summary>
public sealed record GetAuditLogQuery : IRequest<IReadOnlyList<AuditEntryResponse>>;
