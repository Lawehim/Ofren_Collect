namespace OfrenCollect.Application.Dashboard;

/// <summary>The dashboard projection for the current tenant (FR-5.1, FR-5.3).</summary>
public sealed record DashboardResponse(
    IReadOnlyList<DashboardSubscriptionRow> Subscriptions,
    DashboardSummary Summary);

/// <summary>One subscription row: who, what plan, where money is paid, and its status.</summary>
public sealed record DashboardSubscriptionRow(
    Guid SubscriptionId,
    string CustomerName,
    string PlanName,
    decimal PlanAmount,
    string? ReservedAccountNumber,
    string? ReservedBankName,
    DateTimeOffset NextDueDate,
    string Status,
    string? CurrentInvoiceStatus);

/// <summary>Headline figures across the tenant's collections.</summary>
public sealed record DashboardSummary(
    decimal CollectedThisPeriod,
    int OverdueCount,
    int UnmatchedCount);
