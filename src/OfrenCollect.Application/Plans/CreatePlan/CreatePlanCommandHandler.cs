using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Plans;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Application.Plans.CreatePlan;

public sealed class CreatePlanCommandHandler : IRequestHandler<CreatePlanCommand, PlanResponse>
{
    private readonly IPlanRepository _plans;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;

    public CreatePlanCommandHandler(IPlanRepository plans, IUnitOfWork unitOfWork, ITenantContext tenantContext)
    {
        _plans = plans;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
    }

    public async Task<PlanResponse> Handle(CreatePlanCommand command, CancellationToken cancellationToken)
    {
        var plan = Plan.Create(
            _tenantContext.RequireTenantId(), command.Name, Money.Of(command.Amount), command.Interval);

        _plans.Add(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return PlanResponse.From(plan);
    }
}
