using FluentAssertions;
using OfrenCollect.Infrastructure.Ai;

namespace OfrenCollect.Infrastructure.UnitTests.Ai;

public class NullAiAssistantTests
{
    [Fact]
    public async Task Ask_ReturnsUnavailable_NotGrounded()
    {
        var answer = await new NullAiAssistant().AskAsync("how much did I collect?", CancellationToken.None);

        answer.Grounded.Should().BeFalse();
        answer.Answer.Should().Contain("unavailable");
    }
}
