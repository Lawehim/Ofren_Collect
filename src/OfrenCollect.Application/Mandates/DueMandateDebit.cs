using OfrenCollect.SharedKernel;

namespace OfrenCollect.Application.Mandates;

/// <summary>
/// A subscription whose invoice is due and has an active mandate but no debit yet — a candidate for
/// the scheduled auto-debit (FR-9.3). Carries everything needed to initiate the debit with Monnify.
/// </summary>
public sealed record DueMandateDebit(
    Guid TenantId,
    string MandateReference,
    string MonnifyMandateCode,
    Guid InvoiceId,
    Money Amount,
    string CustomerEmail);
