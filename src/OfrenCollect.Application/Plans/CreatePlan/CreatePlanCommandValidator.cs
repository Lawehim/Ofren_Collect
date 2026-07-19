using FluentValidation;

namespace OfrenCollect.Application.Plans.CreatePlan;

public sealed class CreatePlanCommandValidator : AbstractValidator<CreatePlanCommand>
{
    public const int NameMaxLength = 200;

    public CreatePlanCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(NameMaxLength);
        RuleFor(c => c.Amount).GreaterThan(0m);
        RuleFor(c => c.Interval).IsInEnum();
    }
}
