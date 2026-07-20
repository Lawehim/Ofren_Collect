using FluentValidation;
using FluentValidation.Results;
using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Common;
using OfrenCollect.Domain.Refunds;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Application.Refunds.InitiateRefund;

public sealed class InitiateRefundCommandHandler : IRequestHandler<InitiateRefundCommand, RefundResult>
{
    // Monnify requires a credit-alert narration of at most 16 characters.
    private const string RefundNarration = "Ofren refund";

    private readonly IRefundRepository _refunds;
    private readonly IPaymentEventRepository _payments;
    private readonly IMonnifyRefundClient _monnify;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public InitiateRefundCommandHandler(
        IRefundRepository refunds,
        IPaymentEventRepository payments,
        IMonnifyRefundClient monnify,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        TimeProvider clock)
    {
        _refunds = refunds;
        _payments = payments;
        _monnify = monnify;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<RefundResult> Handle(InitiateRefundCommand command, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.RequireTenantId();

        // Idempotency: a redelivered/double-clicked request with the same reference is a no-op that
        // returns the existing refund — never a second refund (FR-11.3).
        var existing = await _refunds.GetByReferenceAsync(command.RefundReference, cancellationToken);
        if (existing is not null)
        {
            return RefundResult.From(existing);
        }

        // Tenant-scoped: a caller can only refund a matched payment it owns. Another tenant's or an
        // unmatched (tenant-less) payment is simply not found (FR-11.1).
        var original = await _payments.GetMatchedByReferenceAsync(command.OriginalTransactionReference, cancellationToken)
            ?? throw new NotFoundException("Original transaction not found.");

        var alreadyRefunded = await _refunds.TotalRefundedForTransactionAsync(
            command.OriginalTransactionReference, cancellationToken);
        var maximumRefundable = original.Amount - Money.Of(alreadyRefunded);

        var amount = Money.Of(command.Amount);
        if (amount > maximumRefundable)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(
                    nameof(command.Amount),
                    "Refund amount exceeds the transaction's remaining refundable amount."),
            });
        }

        var now = _clock.GetUtcNow();
        var refund = Refund.Request(
            tenantId, command.OriginalTransactionReference, command.RefundReference,
            amount, command.Reason, maximumRefundable, now);

        // Call Monnify before persisting: if the provider fails, the command throws and no refund
        // record is saved (same discipline as enrolment). Monnify dedupes on our refund reference,
        // so a retry after a mid-flight crash cannot double-refund.
        var result = await _monnify.InitiateRefundAsync(
            new RefundInitiationRequest(
                command.OriginalTransactionReference, command.RefundReference, amount,
                command.Reason, RefundNarration),
            cancellationToken);

        switch (result.Status)
        {
            case MonnifyRefundStatus.Completed:
                refund.MarkCompleted(now);
                break;
            case MonnifyRefundStatus.Failed:
                refund.MarkFailed(now);
                break;
            case MonnifyRefundStatus.Pending:
            default:
                // Stays Requested until the refund webhook resolves it.
                break;
        }

        _refunds.Add(refund);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return RefundResult.From(refund);
    }
}
