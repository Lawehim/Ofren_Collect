using OfrenCollect.Domain.Plans;

namespace OfrenCollect.Application.Plans;

/// <summary>A plan as returned to the client.</summary>
public sealed record PlanResponse(Guid Id, string Name, decimal Amount, string Interval, bool IsActive)
{
    public static PlanResponse From(Plan plan) =>
        new(plan.Id, plan.Name, plan.Amount.Amount, plan.Interval.ToString(), plan.IsActive);
}
