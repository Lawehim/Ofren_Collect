using FluentValidation;
using OfrenCollect.Application.Auth.Register;

namespace OfrenCollect.Application.Auth.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty();
        RuleFor(c => c.NewPassword).NotEmpty().MinimumLength(RegisterBusinessCommandValidator.PasswordMinLength);
    }
}
