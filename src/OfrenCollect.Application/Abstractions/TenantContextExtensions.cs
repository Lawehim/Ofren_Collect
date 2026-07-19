namespace OfrenCollect.Application.Abstractions;

/// <summary>Helpers for reading the ambient tenant on authenticated paths.</summary>
public static class TenantContextExtensions
{
    /// <summary>
    /// Returns the current tenant, or throws if there is none. Authenticated request handlers
    /// call this — an authenticated request always has a tenant, so its absence is a bug.
    /// </summary>
    public static Guid RequireTenantId(this ITenantContext context) =>
        context.CurrentTenantId ?? throw new InvalidOperationException("No tenant in the current context.");
}
