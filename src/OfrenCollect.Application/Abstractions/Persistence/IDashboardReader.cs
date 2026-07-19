using OfrenCollect.Application.Dashboard;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>
/// Reads the dashboard projection for the current tenant. The implementation is tenant-scoped
/// by the global query filter, so it only ever sees the caller's data.
/// </summary>
public interface IDashboardReader
{
    Task<DashboardResponse> GetAsync(CancellationToken cancellationToken);
}
