using OfrenCollect.Domain.Payments;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Stores payment events and answers the idempotency question.</summary>
public interface IPaymentEventRepository
{
    /// <summary>Whether a payment with this Monnify reference has already been recorded (FR-3.6).</summary>
    Task<bool> ExistsByReferenceAsync(string transactionReference, CancellationToken cancellationToken);

    void Add(PaymentEvent paymentEvent);
}
