using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Common;
using OfrenCollect.Domain.Mandates;

namespace OfrenCollect.Application.Mandates.CancelMandate;

public sealed class CancelMandateCommandHandler : IRequestHandler<CancelMandateCommand>
{
    private readonly IMandateRepository _mandates;
    private readonly IMonnifyMandateClient _monnify;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public CancelMandateCommandHandler(
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

    public async Task Handle(CancelMandateCommand command, CancellationToken cancellationToken)
    {
        _ = _tenantContext.RequireTenantId();

        var mandate = await _mandates.GetByReferenceAsync(command.MandateReference, cancellationToken)
            ?? throw new NotFoundException("Mandate not found.");

        if (mandate.Status == MandateStatus.Revoked)
        {
            return;
        }

        await _monnify.CancelMandateAsync(mandate.MonnifyMandateCode, cancellationToken);
        mandate.Revoke(_clock.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
