using FluentValidation;

namespace OfrenCollect.Application.Auth.ForgotPassword;

public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
    }
}
