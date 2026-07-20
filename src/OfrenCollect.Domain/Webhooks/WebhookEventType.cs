namespace OfrenCollect.Domain.Webhooks;

/// <summary>
/// The kind of Monnify webhook an <see cref="InboxMessage"/> holds, so the drainer can route it to
/// the right handler. Transaction completion drives reconciliation; refund completion resolves a
/// refund's terminal status (FR-11.4).
/// </summary>
public enum WebhookEventType
{
    TransactionCompletion = 0,
    RefundCompletion
}
