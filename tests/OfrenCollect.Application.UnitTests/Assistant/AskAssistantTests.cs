using FluentAssertions;
using FluentValidation.TestHelper;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Assistant;
using OfrenCollect.Application.Assistant.AskAssistant;

namespace OfrenCollect.Application.UnitTests.Assistant;

public class AskAssistantTests
{
    [Fact]
    public async Task Handler_DelegatesToTheAssistant_AndReturnsItsAnswer()
    {
        var assistant = Substitute.For<IAiAssistant>();
        var expected = new AssistantAnswer("You've collected ₦10,000 this week.", Grounded: true, "CollectedThisWeek");
        assistant.AskAsync("how much this week?", Arg.Any<CancellationToken>()).Returns(expected);

        var result = await new AskAssistantQueryHandler(assistant).Handle(
            new AskAssistantQuery("how much this week?"), CancellationToken.None);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_RejectsBlankQuestion(string question)
    {
        var result = new AskAssistantQueryValidator().TestValidate(new AskAssistantQuery(question));

        result.ShouldHaveValidationErrorFor(q => q.Question);
    }

    [Fact]
    public void Validator_RejectsQuestionOverMaxLength()
    {
        var tooLong = new string('a', AskAssistantQueryValidator.QuestionMaxLength + 1);

        var result = new AskAssistantQueryValidator().TestValidate(new AskAssistantQuery(tooLong));

        result.ShouldHaveValidationErrorFor(q => q.Question);
    }

    [Fact]
    public void Validator_AcceptsAReasonableQuestion()
    {
        var result = new AskAssistantQueryValidator().TestValidate(new AskAssistantQuery("who is overdue?"));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
