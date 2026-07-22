using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Mandates;

namespace OfrenCollect.Application.Mandates.ResolveMandateStatus;

/// <summary>
/// Applies a mandate webhook by re-verifying the status with Monnify, never trusting the body (§8).
/// Runs with no ambient tenant, so it uses a filter-bypassing lookup. Idempotent: activation only
/// from Pending, revocation only when not already terminal, so a redelivered event is a safe no-op.
/// </summary>
public sealed class ResolveMandateStatusCommandHandler : IRequestHandler<ResolveMandateStatusCommand>
{
    private readonly IMandateRepository _mandates;
    private readonly IMonnifyMandateClient _monnify;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public ResolveMandateStatusCommandHandler(
        IMandateRepository mandates, IMonnifyMandateClient monnify, IUnitOfWork unitOfWork, TimeProvider clock)
    {
        _mandates = mandates;
        _monnify = monnify;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task Handle(ResolveMandateStatusCommand command, CancellationToken cancellationToken)
    {
        var mandate = await _mandates.GetForResolutionAsync(command.MandateReference, cancellationToken);
        if (mandate is null)
        {
            return;
        }

        var status = await _monnify.GetMandateStatusAsync(command.MandateReference, cancellationToken);
        var now = _clock.GetUtcNow();

        switch (status)
        {
            case MonnifyMandateStatus.Active when mandate.Status == MandateStatus.Pending:
                mandate.Activate(now);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                break;
            case MonnifyMandateStatus.Cancelled or MonnifyMandateStatus.Expired
                when mandate.Status is MandateStatus.Pending or MandateStatus.Active:
                mandate.Revoke(now);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                break;
            default:
                // No actionable change (still initiated, or already in the target state).
                break;
        }
    }
}
