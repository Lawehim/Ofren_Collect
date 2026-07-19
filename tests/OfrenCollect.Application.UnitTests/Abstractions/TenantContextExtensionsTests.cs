using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions;

namespace OfrenCollect.Application.UnitTests.Abstractions;

public class TenantContextExtensionsTests
{
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    [Fact]
    public void RequireTenantId_WhenTenantPresent_ReturnsIt()
    {
        var tenantId = Guid.NewGuid();
        _tenantContext.CurrentTenantId.Returns(tenantId);

        _tenantContext.RequireTenantId().Should().Be(tenantId);
    }

    [Fact]
    public void RequireTenantId_WhenNoTenant_Throws()
    {
        _tenantContext.CurrentTenantId.Returns((Guid?)null);

        var act = () => _tenantContext.RequireTenantId();

        act.Should().Throw<InvalidOperationException>();
    }
}
