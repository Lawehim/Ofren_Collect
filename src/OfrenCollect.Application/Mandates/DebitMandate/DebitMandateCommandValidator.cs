using FluentValidation;

namespace OfrenCollect.Application.Mandates.DebitMandate;

public sealed class DebitMandateCommandValidator : AbstractValidator<DebitMandateCommand>
{
    public DebitMandateCommandValidator() => RuleFor(c => c.SubscriptionId).NotEmpty();
}
