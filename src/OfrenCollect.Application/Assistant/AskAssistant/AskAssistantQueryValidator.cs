using FluentValidation;

namespace OfrenCollect.Application.Assistant.AskAssistant;

public sealed class AskAssistantQueryValidator : AbstractValidator<AskAssistantQuery>
{
    public const int QuestionMaxLength = 500;

    public AskAssistantQueryValidator()
    {
        RuleFor(q => q.Question).NotEmpty().MaximumLength(QuestionMaxLength);
    }
}
