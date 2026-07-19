using FluentAssertions;
using OfrenCollect.Domain.Plans;

namespace OfrenCollect.Domain.UnitTests.Plans;

public class BillingIntervalTests
{
    private static readonly DateTimeOffset From = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Weekly_AdvancesBySevenDays()
    {
        BillingInterval.Weekly.NextDueDateFrom(From)
            .Should().Be(new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Monthly_AdvancesByOneMonth()
    {
        BillingInterval.Monthly.NextDueDateFrom(From)
            .Should().Be(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Yearly_AdvancesByOneYear()
    {
        BillingInterval.Yearly.NextDueDateFrom(From)
            .Should().Be(new DateTimeOffset(2027, 7, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void UnknownInterval_Throws()
    {
        var act = () => ((BillingInterval)999).NextDueDateFrom(From);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
