using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Assistant;

namespace OfrenCollect.Application.UnitTests.Assistant;

public class AiAssistantTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly IIntentClassifier _classifier = Substitute.For<IIntentClassifier>();
    private readonly IAssistantData _data = Substitute.For<IAssistantData>();

    private AiAssistant CreateAssistant() => new(_classifier, _data, new FixedClock(Now));

    private void GivenIntent(CollectionsIntent intent) =>
        _classifier.ClassifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(intent);

    [Fact]
    public async Task Ask_CollectedThisWeek_ReturnsGroundedAmount_FromStartOfWeek()
    {
        GivenIntent(CollectionsIntent.CollectedThisWeek);
        _data.CollectedSinceAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(2_415_000m);

        var answer = await CreateAssistant().AskAsync("how much did I collect this week?", CancellationToken.None);

        answer.Grounded.Should().BeTrue();
        answer.Answer.Should().Contain("2,415,000");
        await _data.Received(1).CollectedSinceAsync(
            Arg.Is<DateTimeOffset>(d =>
                d.DayOfWeek == DayOfWeek.Monday && d.TimeOfDay == TimeSpan.Zero
                && d <= Now && (Now - d) < TimeSpan.FromDays(7)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ask_Overdue_ReturnsGroundedCount()
    {
        GivenIntent(CollectionsIntent.OverdueCustomers);
        _data.OverdueSubscriptionCountAsync(Arg.Any<CancellationToken>()).Returns(3);

        var answer = await CreateAssistant().AskAsync("who's overdue?", CancellationToken.None);

        answer.Grounded.Should().BeTrue();
        answer.Answer.Should().Contain("3");
        answer.Answer.Should().Contain("overdue");
    }

    [Fact]
    public async Task Ask_Unknown_Declines_WithoutInventing_AndTouchesNoData()
    {
        GivenIntent(CollectionsIntent.Unknown);

        var answer = await CreateAssistant().AskAsync("cancel Chidi's subscription", CancellationToken.None);

        answer.Grounded.Should().BeFalse();
        answer.Answer.Should().Contain("can't answer");
        await _data.DidNotReceive().CollectedSinceAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _data.DidNotReceive().OverdueSubscriptionCountAsync(Arg.Any<CancellationToken>());
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
