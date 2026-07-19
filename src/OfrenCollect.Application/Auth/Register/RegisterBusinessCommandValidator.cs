using FluentValidation;

namespace OfrenCollect.Application.Auth.Register;

public sealed class RegisterBusinessCommandValidator : AbstractValidator<RegisterBusinessCommand>
{
    public const int PasswordMinLength = 8;
    public const int BusinessNameMaxLength = 200;

    public RegisterBusinessCommandValidator()
    {
        RuleFor(c => c.BusinessName)
            .NotEmpty()
            .MaximumLength(BusinessNameMaxLength);

        RuleFor(c => c.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(c => c.Password)
            .NotEmpty()
            .MinimumLength(PasswordMinLength);
    }
}
