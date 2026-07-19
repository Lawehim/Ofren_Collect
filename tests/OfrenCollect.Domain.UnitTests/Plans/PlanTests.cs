using FluentAssertions;
using OfrenCollect.Domain.Plans;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.UnitTests.Plans;

public class PlanTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static Plan NewPlan(string name = "Basic", decimal amount = 5000m) =>
        Plan.Create(TenantId, name, Money.Of(amount), BillingInterval.Monthly);

    [Fact]
    public void Create_WithValidData_SetsFieldsAndIsActive()
    {
        var plan = NewPlan();

        plan.TenantId.Should().Be(TenantId);
        plan.Name.Should().Be("Basic");
        plan.Amount.Should().Be(Money.Of(5000m));
        plan.Interval.Should().Be(BillingInterval.Monthly);
        plan.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WithZeroAmount_Throws()
    {
        var act = () => NewPlan(amount: 0m);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_Throws(string name)
    {
        var act = () => NewPlan(name: name);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_MakesPlanInactive()
    {
        var plan = NewPlan();

        plan.Deactivate();

        plan.IsActive.Should().BeFalse();
    }
}
