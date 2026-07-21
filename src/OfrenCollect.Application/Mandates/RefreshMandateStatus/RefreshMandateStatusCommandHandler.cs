using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Common;
using OfrenCollect.Domain.Mandates;

namespace OfrenCollect.Application.Mandates.RefreshMandateStatus;

public sealed class RefreshMandateStatusCommandHandler
    : IRequestHandler<RefreshMandateStatusCommand, MandateStatusResult>
{
    private readonly IMandateRepository _mandates;
    private readonly IMonnifyMandateClient _monnify;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public RefreshMandateStatusCommandHandler(
        IMandateRepository mandates,
        IMonnifyMandateClient monnify,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        TimeProvider clock)
    {
        _mandates = mandates;
        _monnify = monnify;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<MandateStatusResult> Handle(RefreshMandateStatusCommand command, CancellationToken cancellationToken)
    {
        _ = _tenantContext.RequireTenantId();

        // Tenant-scoped: a caller can only refresh a mandate it owns.
        var mandate = await _mandates.GetByReferenceAsync(command.MandateReference, cancellationToken)
            ?? throw new NotFoundException("Mandate not found.");

        // Only a still-pending mandate needs a re-check; a terminal one is returned as-is.
        if (mandate.Status == MandateStatus.Pending)
        {
            var status = await _monnify.GetMandateStatusAsync(command.MandateReference, cancellationToken);
            var now = _clock.GetUtcNow();
            switch (status)
            {
                case MonnifyMandateStatus.Active:
                    mandate.Activate(now);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    break;
                case MonnifyMandateStatus.Cancelled:
                    mandate.Revoke(now);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    break;
                default:
                    // Still initiated / failed / expired / unknown — leave pending for the next check.
                    break;
            }
        }

        return new MandateStatusResult(mandate.MandateReference, mandate.Status.ToString());
    }
}
