using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Invoices;
using OfrenCollect.Domain.Subscriptions;

namespace OfrenCollect.Repository.Persistence;

/// <summary>
/// Tenant-scoped reads that ground the assistant's answers. Subscriptions/invoices are scoped by
/// the global query filter; PaymentEvents (nullable tenant) are scoped explicitly.
/// </summary>
public sealed class AssistantDataReader : IAssistantData
{
    private readonly OfrenDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AssistantDataReader(OfrenDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<decimal> CollectedSinceAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.CurrentTenantId;
        var amounts = await _db.PaymentEvents
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.PaidAt >= since)
            .Select(p => p.Amount.Amount)
            .ToListAsync(cancellationToken);

        return amounts.Sum();
    }

    public Task<int> OverdueSubscriptionCountAsync(CancellationToken cancellationToken) =>
        _db.Subscriptions.AsNoTracking().CountAsync(s => s.Status == SubscriptionStatus.Overdue, cancellationToken);

    public Task<int> UnderpaidInvoiceCountAsync(CancellationToken cancellationToken) =>
        _db.Invoices.AsNoTracking().CountAsync(i => i.Status == InvoiceStatus.Underpaid, cancellationToken);

    public Task<int> ActiveSubscriptionCountAsync(CancellationToken cancellationToken) =>
        _db.Subscriptions.AsNoTracking().CountAsync(s => s.Status == SubscriptionStatus.Active, cancellationToken);

    public Task<int> UnmatchedPaymentCountAsync(CancellationToken cancellationToken) =>
        _db.PaymentEvents.IgnoreQueryFilters().AsNoTracking().CountAsync(p => p.TenantId == null, cancellationToken);
}
