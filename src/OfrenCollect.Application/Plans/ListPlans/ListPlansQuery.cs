using MediatR;

namespace OfrenCollect.Application.Plans.ListPlans;

/// <summary>Lists the current tenant's plans (FR-1.2).</summary>
public sealed record ListPlansQuery : IRequest<IReadOnlyList<PlanResponse>>;
