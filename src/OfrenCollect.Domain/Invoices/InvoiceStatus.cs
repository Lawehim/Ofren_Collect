namespace OfrenCollect.Domain.Invoices;

/// <summary>
/// The payment status of an invoice, driven purely by how much has been paid against it.
/// "Overdue" is intentionally not here: it is a time-based condition of a subscription
/// (set by the overdue sweeper), not a payment outcome of a single invoice.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Issued, nothing paid yet.</summary>
    Pending = 0,

    /// <summary>Some but not all of the amount due has been paid.</summary>
    Underpaid = 1,

    /// <summary>Exactly the amount due has been paid.</summary>
    Paid = 2,

    /// <summary>More than the amount due has been paid; the surplus is recorded as credit.</summary>
    Overpaid = 3
}
