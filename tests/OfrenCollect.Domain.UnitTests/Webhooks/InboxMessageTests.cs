using FluentAssertions;
using OfrenCollect.Domain.Webhooks;

namespace OfrenCollect.Domain.UnitTests.Webhooks;

public class InboxMessageTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 7, 19, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Receive_StoresDetails_AndIsUnprocessed()
    {
        var message = InboxMessage.Receive("MNFY|1", "4003115967", "{\"raw\":true}", ReceivedAt);

        message.TransactionReference.Should().Be("MNFY|1");
        message.DestinationAccountNumber.Should().Be("4003115967");
        message.RawPayload.Should().Be("{\"raw\":true}");
        message.ReceivedAt.Should().Be(ReceivedAt);
        message.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public void MarkProcessed_SetsProcessedAt()
    {
        var message = InboxMessage.Receive("MNFY|1", "4003115967", "{}", ReceivedAt);

        message.MarkProcessed(ReceivedAt.AddSeconds(3));

        message.IsProcessed.Should().BeTrue();
        message.ProcessedAt.Should().Be(ReceivedAt.AddSeconds(3));
    }

    [Theory]
    [InlineData("", "4003115967")]
    [InlineData("MNFY|1", "")]
    public void Receive_WithBlankReferenceOrAccount_Throws(string reference, string account)
    {
        var act = () => InboxMessage.Receive(reference, account, "{}", ReceivedAt);

        act.Should().Throw<ArgumentException>();
    }
}
