using MediatR;

namespace OfrenCollect.Application.Dashboard.GetDashboard;

/// <summary>Returns the dashboard projection for the current tenant (FR-5.1).</summary>
public sealed record GetDashboardQuery : IRequest<DashboardResponse>;
