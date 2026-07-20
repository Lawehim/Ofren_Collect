using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Refunds;

namespace OfrenCollect.Application.Refunds.ResolveRefund;

/// <summary>
/// Resolves a refund after its webhook fires (FR-11.4). The webhook body is not trusted — the
/// status is re-verified with Monnify (§8), exactly as transaction reconciliation re-verifies the
/// payment. Idempotent and safe against contradictory redeliveries: it acts only on a
/// still-<see cref="RefundStatus.Requested"/> refund and only on a terminal Monnify status, so a
/// redelivered event or a not-yet-terminal re-query is a harmless no-op and never wedges the drainer.
/// </summary>
public sealed class ResolveRefundCommandHandler : IRequestHandler<ResolveRefundCommand>
{
    private readonly IRefundRepository _refunds;
    private readonly IMonnifyRefundClient _monnify;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public ResolveRefundCommandHandler(
        IRefundRepository refunds,
        IMonnifyRefundClient monnify,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _refunds = refunds;
        _monnify = monnify;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task Handle(ResolveRefundCommand command, CancellationToken cancellationToken)
    {
        var refund = await _refunds.GetForResolutionAsync(command.RefundReference, cancellationToken);
        if (refund is null || refund.Status != RefundStatus.Requested)
        {
            return;
        }

        var status = await _monnify.GetRefundStatusAsync(command.RefundReference, cancellationToken);

        var now = _clock.GetUtcNow();
        switch (status)
        {
            case MonnifyRefundStatus.Completed:
                refund.MarkCompleted(now);
                break;
            case MonnifyRefundStatus.Failed:
                refund.MarkFailed(now);
                break;
            case MonnifyRefundStatus.Pending:
            default:
                // Not terminal yet — leave it Requested; a later webhook re-drains it.
                return;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
