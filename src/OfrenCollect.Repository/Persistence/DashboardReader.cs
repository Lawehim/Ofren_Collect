using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Dashboard;
using OfrenCollect.Domain.Subscriptions;

namespace OfrenCollect.Repository.Persistence;

/// <summary>
/// Builds the dashboard projection for the current tenant. Reads are tenant-scoped by the
/// global query filter; only the unmatched-inflow count reaches across (those are tenant-less
/// orphans, not another tenant's data).
/// </summary>
public sealed class DashboardReader : IDashboardReader
{
    private readonly OfrenDbContext _db;

    public DashboardReader(OfrenDbContext db) => _db = db;

    public async Task<DashboardResponse> GetAsync(CancellationToken cancellationToken)
    {
        var subscriptions = await _db.Subscriptions.AsNoTracking().ToListAsync(cancellationToken);
        var customers = await _db.Customers.AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
        var plans = await _db.Plans.AsNoTracking().ToDictionaryAsync(p => p.Id, cancellationToken);
        var invoices = await _db.Invoices.AsNoTracking().ToListAsync(cancellationToken);

        var latestInvoiceBySubscription = invoices
            .GroupBy(i => i.SubscriptionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.DueDate).First());

        var rows = subscriptions
            .Select(subscription =>
            {
                customers.TryGetValue(subscription.CustomerId, out var customer);
                plans.TryGetValue(subscription.PlanId, out var plan);
                latestInvoiceBySubscription.TryGetValue(subscription.Id, out var invoice);

                return new DashboardSubscriptionRow(
                    subscription.Id,
                    customer?.Name ?? string.Empty,
                    plan?.Name ?? string.Empty,
                    plan?.Amount.Amount ?? 0m,
                    subscription.ReservedAccountNumber,
                    subscription.ReservedBankName,
                    subscription.NextDueDate,
                    subscription.Status.ToString(),
                    invoice?.Status.ToString());
            })
            .ToList();

        var collectedThisPeriod = invoices.Sum(i => i.AmountPaid.Amount);
        var overdueCount = subscriptions.Count(s => s.Status == SubscriptionStatus.Overdue);
        var unmatchedCount = await _db.PaymentEvents
            .IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == null, cancellationToken);

        return new DashboardResponse(rows, new DashboardSummary(collectedThisPeriod, overdueCount, unmatchedCount));
    }
}
