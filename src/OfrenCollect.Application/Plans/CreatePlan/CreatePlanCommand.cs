using MediatR;
using OfrenCollect.Domain.Plans;

namespace OfrenCollect.Application.Plans.CreatePlan;

/// <summary>Creates a billing plan for the current tenant (FR-1.1).</summary>
public sealed record CreatePlanCommand(string Name, decimal Amount, BillingInterval Interval)
    : IRequest<PlanResponse>;
