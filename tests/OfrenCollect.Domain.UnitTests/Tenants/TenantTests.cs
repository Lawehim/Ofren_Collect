using FluentAssertions;
using OfrenCollect.Domain.Tenants;

namespace OfrenCollect.Domain.UnitTests.Tenants;

public class TenantTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 7, 19, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Register_WithValidName_SetsFields()
    {
        var tenant = Tenant.Register("BrightPath Tutors", CreatedAt);

        tenant.BusinessName.Should().Be("BrightPath Tutors");
        tenant.CreatedAt.Should().Be(CreatedAt);
        tenant.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_WithBlankName_Throws(string name)
    {
        var act = () => Tenant.Register(name, CreatedAt);

        act.Should().Throw<ArgumentException>();
    }
}
