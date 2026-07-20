using FluentValidation;

namespace OfrenCollect.Application.Refunds.InitiateRefund;

public sealed class InitiateRefundCommandValidator : AbstractValidator<InitiateRefundCommand>
{
    /// <summary>Monnify's documented minimum refund amount is ₦100.</summary>
    public const decimal MinimumRefundNaira = 100m;

    /// <summary>Monnify caps the internal refund reason at 64 characters.</summary>
    public const int ReasonMaxLength = 64;

    public InitiateRefundCommandValidator()
    {
        RuleFor(c => c.OriginalTransactionReference).NotEmpty();
        RuleFor(c => c.RefundReference).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(ReasonMaxLength);
        RuleFor(c => c.Amount)
            .GreaterThanOrEqualTo(MinimumRefundNaira)
            .Must(amount => amount == decimal.Round(amount, 2))
            .WithMessage("'Amount' cannot have more than two decimal places.");
    }
}
