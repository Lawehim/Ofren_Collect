using FluentAssertions;
using OfrenCollect.Domain.Customers;

namespace OfrenCollect.Domain.UnitTests.Customers;

public class CustomerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Register_WithValidData_SetsFields()
    {
        var customer = Customer.Register(TenantId, "Chidi Eze", "chidi@mail.com");

        customer.TenantId.Should().Be(TenantId);
        customer.Name.Should().Be("Chidi Eze");
        customer.Email.Should().Be("chidi@mail.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_WithBlankName_Throws(string name)
    {
        var act = () => Customer.Register(TenantId, name, "chidi@mail.com");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_WithBlankEmail_Throws(string email)
    {
        var act = () => Customer.Register(TenantId, "Chidi Eze", email);

        act.Should().Throw<ArgumentException>();
    }
}
