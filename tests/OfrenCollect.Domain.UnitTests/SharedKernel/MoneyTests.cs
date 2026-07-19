using FluentAssertions;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.UnitTests.SharedKernel;

public class MoneyTests
{
    [Fact]
    public void Of_WithValidAmount_SetsAmountAndCurrency()
    {
        var money = Money.Of(5000m, Currency.Ngn);

        money.Amount.Should().Be(5000m);
        money.Currency.Should().Be(Currency.Ngn);
    }

    [Fact]
    public void Of_WithoutCurrency_DefaultsToNgn()
    {
        var money = Money.Of(5000m);

        money.Currency.Should().Be(Currency.Ngn);
    }

    [Fact]
    public void Of_WithTwoDecimalPlaces_IsAllowed()
    {
        var money = Money.Of(4999.99m);

        money.Amount.Should().Be(4999.99m);
    }

    [Fact]
    public void Of_WithZeroAmount_IsAllowed()
    {
        var money = Money.Of(0m);

        money.Amount.Should().Be(0m);
    }

    [Fact]
    public void Of_WithNegativeAmount_Throws()
    {
        var act = () => Money.Of(-1m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Of_WithMoreThanTwoDecimalPlaces_Throws()
    {
        var act = () => Money.Of(5000.001m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Zero_HasZeroAmountInGivenCurrency()
    {
        var zero = Money.Zero(Currency.Ngn);

        zero.Amount.Should().Be(0m);
        zero.Currency.Should().Be(Currency.Ngn);
    }

    [Fact]
    public void Add_WithSameCurrency_SumsAmounts()
    {
        var result = Money.Of(3000m) + Money.Of(2000m);

        result.Should().Be(Money.Of(5000m));
    }

    [Fact]
    public void Add_WithDifferentCurrency_Throws()
    {
        var act = () => Money.Of(1000m, Currency.Ngn) + Money.Of(1000m, Currency.Usd);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Subtract_WithSameCurrency_ReturnsDifference()
    {
        var result = Money.Of(5000m) - Money.Of(2000m);

        result.Should().Be(Money.Of(3000m));
    }

    [Fact]
    public void Subtract_ResultingInNegative_Throws()
    {
        var act = () => Money.Of(2000m) - Money.Of(5000m);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Subtract_WithDifferentCurrency_Throws()
    {
        var act = () => Money.Of(5000m, Currency.Ngn) - Money.Of(1000m, Currency.Usd);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GreaterThan_WhenLeftExceedsRight_IsTrue()
    {
        (Money.Of(6000m) > Money.Of(5000m)).Should().BeTrue();
        (Money.Of(5000m) > Money.Of(5000m)).Should().BeFalse();
    }

    [Fact]
    public void LessThan_WhenLeftBelowRight_IsTrue()
    {
        (Money.Of(4000m) < Money.Of(5000m)).Should().BeTrue();
        (Money.Of(5000m) < Money.Of(5000m)).Should().BeFalse();
    }

    [Fact]
    public void GreaterThanOrEqual_WhenEqual_IsTrue()
    {
        (Money.Of(5000m) >= Money.Of(5000m)).Should().BeTrue();
    }

    [Fact]
    public void LessThanOrEqual_WhenEqual_IsTrue()
    {
        (Money.Of(5000m) <= Money.Of(5000m)).Should().BeTrue();
    }

    [Fact]
    public void Comparison_WithDifferentCurrency_Throws()
    {
        var act = () => _ = Money.Of(5000m, Currency.Ngn) > Money.Of(1000m, Currency.Usd);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Equality_WithSameAmountAndCurrency_AreEqual()
    {
        Money.Of(5000m).Should().Be(Money.Of(5000m));
    }

    [Fact]
    public void Equality_IgnoresTrailingZeroScale()
    {
        Money.Of(5000m).Should().Be(Money.Of(5000.00m));
    }

    [Fact]
    public void Equality_WithDifferentAmount_AreNotEqual()
    {
        Money.Of(5000m).Should().NotBe(Money.Of(4000m));
    }

    [Fact]
    public void ToString_IncludesCurrencyAndTwoDecimalPlaces()
    {
        Money.Of(5000m).ToString().Should().Be("NGN 5000.00");
    }
}
