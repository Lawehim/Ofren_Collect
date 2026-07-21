using FluentValidation;

namespace OfrenCollect.Application.Mandates.CreateMandate;

public sealed class CreateMandateCommandValidator : AbstractValidator<CreateMandateCommand>
{
    public CreateMandateCommandValidator()
    {
        RuleFor(c => c.SubscriptionId).NotEmpty();
        RuleFor(c => c.CustomerAccountNumber).NotEmpty();
        RuleFor(c => c.CustomerAccountBankCode).NotEmpty();
        RuleFor(c => c.CustomerAddress).NotEmpty();
        RuleFor(c => c.CustomerPhoneNumber).NotEmpty();
    }
}
