using FluentValidation;

namespace OfrenCollect.Application.Customers.RegisterCustomer;

public sealed class RegisterCustomerCommandValidator : AbstractValidator<RegisterCustomerCommand>
{
    public const int NameMaxLength = 200;

    public RegisterCustomerCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(NameMaxLength);
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
    }
}
