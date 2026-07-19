using Microsoft.Extensions.DependencyInjection;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Domain.Audit;

namespace OfrenCollect.Repository.Persistence.Repositories;

/// <summary>
/// Persists audit entries in their own DbContext scope, so recording a call is independent of
/// the request's unit of work (its business changes are neither committed nor lost by auditing).
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AuditLogger(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OfrenDbContext>();
        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }
}
