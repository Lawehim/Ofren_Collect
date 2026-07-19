using FluentValidation;

namespace OfrenCollect.Application.Subscriptions.EnrolCustomer;

public sealed class EnrolCustomerCommandValidator : AbstractValidator<EnrolCustomerCommand>
{
    public EnrolCustomerCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.PlanId).NotEmpty();
    }
}
