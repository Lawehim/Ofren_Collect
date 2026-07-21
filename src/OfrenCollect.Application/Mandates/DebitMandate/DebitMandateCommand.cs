using MediatR;

namespace OfrenCollect.Application.Mandates.DebitMandate;

/// <summary>
/// Debits a subscription's active mandate for its open invoice (FR-9.3). Returns the debit's payment
/// reference; the debit completes asynchronously and is then reconciled to the invoice.
/// </summary>
public sealed record DebitMandateCommand(Guid SubscriptionId) : IRequest<MandateDebitInitiatedResult>;

/// <summary>The initiated debit: our payment reference and Monnify's initial status.</summary>
public sealed record MandateDebitInitiatedResult(string PaymentReference, string Status);
