using MediatR;

namespace OfrenCollect.Application.Mandates.ReconcileMandateDebit;

/// <summary>
/// Re-checks a mandate debit with Monnify and, if paid, applies it to the invoice (FR-9.3, FR-9.4).
/// This is the debit's reconciliation path: it resolves the invoice from the debit record (not from
/// a reserved account) and reuses the same apply-payment logic.
/// </summary>
public sealed record ReconcileMandateDebitCommand(string PaymentReference) : IRequest<MandateDebitStatusResult>;

/// <summary>The debit's payment reference and current status.</summary>
public sealed record MandateDebitStatusResult(string PaymentReference, string Status);
