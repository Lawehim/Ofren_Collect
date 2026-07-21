using OfrenCollect.Domain.Mandates;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Stores mandate debits and answers the idempotency/reconciliation lookup (FR-9.3).</summary>
public interface IMandateDebitRepository
{
    void Add(MandateDebit debit);

    /// <summary>A debit by its payment reference (tenant-scoped, tracked so it can be resolved).</summary>
    Task<MandateDebit?> GetByPaymentReferenceAsync(string paymentReference, CancellationToken cancellationToken);

    /// <summary>Whether a non-failed debit already exists for an invoice — stops double-charging it.</summary>
    Task<bool> HasActiveDebitForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken);

    /// <summary>
    /// The oldest still-pending debits across all tenants, tracked — for the background drainer,
    /// which has no ambient tenant, so the implementation bypasses the global filter.
    /// </summary>
    Task<IReadOnlyList<MandateDebit>> GetPendingAsync(int limit, CancellationToken cancellationToken);
}
