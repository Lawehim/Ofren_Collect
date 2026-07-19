using MediatR;
using OfrenCollect.Application.Abstractions.Persistence;

namespace OfrenCollect.Application.Plans.ListPlans;

public sealed class ListPlansQueryHandler : IRequestHandler<ListPlansQuery, IReadOnlyList<PlanResponse>>
{
    private readonly IPlanRepository _plans;

    public ListPlansQueryHandler(IPlanRepository plans) => _plans = plans;

    public async Task<IReadOnlyList<PlanResponse>> Handle(ListPlansQuery query, CancellationToken cancellationToken)
    {
        var plans = await _plans.ListAsync(cancellationToken);
        return plans.Select(PlanResponse.From).ToList();
    }
}
