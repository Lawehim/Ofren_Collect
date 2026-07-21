using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Mandates;
using OfrenCollect.Domain.Invoices;
using OfrenCollect.Domain.Mandates;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Repository.Persistence;

/// <summary>
/// Finds invoices due for auto-debit (FR-9.3): an active mandate whose subscription has a due, open
/// invoice with no active debit yet. Runs on the background path with no ambient tenant, so it
/// bypasses the global filter and carries each row's own tenant.
/// </summary>
public sealed class DueMandateDebitReader : IDueMandateDebitReader
{
    private readonly OfrenDbContext _db;

    public DueMandateDebitReader(OfrenDbContext db) => _db = db;

    public async Task<IReadOnlyList<DueMandateDebit>> GetDueAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken)
    {
        var rows = await (
            from mandate in _db.Mandates.IgnoreQueryFilters()
            where mandate.Status == MandateStatus.Active
            join subscription in _db.Subscriptions.IgnoreQueryFilters()
                on mandate.SubscriptionId equals subscription.Id
            join invoice in _db.Invoices.IgnoreQueryFilters()
                on subscription.Id equals invoice.SubscriptionId
            join customer in _db.Customers.IgnoreQueryFilters()
                on subscription.CustomerId equals customer.Id
            where (invoice.Status == InvoiceStatus.Pending || invoice.Status == InvoiceStatus.Underpaid)
                && invoice.DueDate <= asOf
                && !_db.MandateDebits.IgnoreQueryFilters()
                    .Any(d => d.InvoiceId == invoice.Id && d.Status != MandateDebitStatus.Failed)
            orderby invoice.DueDate
            select new
            {
                mandate.TenantId,
                mandate.MandateReference,
                mandate.MonnifyMandateCode,
                InvoiceId = invoice.Id,
                AmountDue = invoice.AmountDue.Amount,
                AmountPaid = invoice.AmountPaid.Amount,
                customer.Email,
            })
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows
            .Where(r => r.AmountDue - r.AmountPaid > 0m)
            .Select(r => new DueMandateDebit(
                r.TenantId, r.MandateReference, r.MonnifyMandateCode, r.InvoiceId,
                Money.Of(r.AmountDue - r.AmountPaid), r.Email))
            .ToList();
    }
}
