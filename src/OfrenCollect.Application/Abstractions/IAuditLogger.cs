using OfrenCollect.Domain.Audit;

namespace OfrenCollect.Application.Abstractions;

/// <summary>
/// Persists an audit entry independently of the request's unit of work, so recording a call
/// never commits (or is rolled back with) that request's business changes.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken);
}
