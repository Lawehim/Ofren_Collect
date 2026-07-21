namespace OfrenCollect.Domain.Mandates;

/// <summary>The lifecycle of a single mandate debit: initiated, then resolved to paid or failed.</summary>
public enum MandateDebitStatus
{
    Pending = 0,
    Paid,
    Failed
}
