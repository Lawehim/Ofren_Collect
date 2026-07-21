using OfrenCollect.Application.Mandates;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Finds invoices due for auto-debit under an active mandate (FR-9.3), across all tenants.</summary>
public interface IDueMandateDebitReader
{
    Task<IReadOnlyList<DueMandateDebit>> GetDueAsync(DateTimeOffset asOf, int limit, CancellationToken cancellationToken);
}
