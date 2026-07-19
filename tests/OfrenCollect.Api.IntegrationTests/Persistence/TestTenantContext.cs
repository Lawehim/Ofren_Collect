using OfrenCollect.Application.Abstractions;

namespace OfrenCollect.Api.IntegrationTests.Persistence;

/// <summary>An <see cref="ITenantContext"/> with a fixed tenant, for driving isolation tests.</summary>
internal sealed class TestTenantContext : ITenantContext
{
    public TestTenantContext(Guid? tenantId) => CurrentTenantId = tenantId;

    public Guid? CurrentTenantId { get; }
}
