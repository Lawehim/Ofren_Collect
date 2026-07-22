using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.Webhooks;

/// <summary>
/// A verified webhook notification persisted durably before it is acknowledged (NFR-2.6). The
/// background drainer processes unprocessed messages and marks them done, so a crash
/// mid-processing cannot lose an already-acknowledged notification — it is simply re-drained (the
/// processing is idempotent). One message holds exactly one <see cref="WebhookEventType"/>; the
/// fields populated depend on it.
/// </summary>
public sealed class InboxMessage : Entity
{
    private InboxMessage()
    {
    }

    private InboxMessage(
        Guid id,
        WebhookEventType eventType,
        string? transactionReference,
        string? destinationAccountNumber,
        string? refundReference,
        string? mandateReference,
        string rawPayload,
        DateTimeOffset receivedAt)
        : base(id)
    {
        EventType = eventType;
        TransactionReference = transactionReference;
        DestinationAccountNumber = destinationAccountNumber;
        RefundReference = refundReference;
        MandateReference = mandateReference;
        RawPayload = rawPayload;
        ReceivedAt = receivedAt;
    }

    public WebhookEventType EventType { get; private set; }

    /// <summary>The Monnify transaction reference — set for <see cref="WebhookEventType.TransactionCompletion"/>.</summary>
    public string? TransactionReference { get; private set; }

    /// <summary>The reserved account paid into — set for <see cref="WebhookEventType.TransactionCompletion"/>.</summary>
    public string? DestinationAccountNumber { get; private set; }

    /// <summary>Our refund reference — set for <see cref="WebhookEventType.RefundCompletion"/>.</summary>
    public string? RefundReference { get; private set; }

    /// <summary>Our mandate reference — set for <see cref="WebhookEventType.MandateStatusChange"/>.</summary>
    public string? MandateReference { get; private set; }

    public string RawPayload { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    public bool IsProcessed => ProcessedAt.HasValue;

    /// <summary>Records a transaction-completion notification to be reconciled (FR-3.2).</summary>
    public static InboxMessage Receive(
        string transactionReference,
        string destinationAccountNumber,
        string rawPayload,
        DateTimeOffset receivedAt)
    {
        Guard.AgainstNullOrWhiteSpace(transactionReference, nameof(transactionReference));
        Guard.AgainstNullOrWhiteSpace(destinationAccountNumber, nameof(destinationAccountNumber));

        return new InboxMessage(
            Guid.NewGuid(), WebhookEventType.TransactionCompletion,
            transactionReference.Trim(), destinationAccountNumber.Trim(),
            refundReference: null, mandateReference: null, rawPayload, receivedAt);
    }

    /// <summary>
    /// Records a refund-completion notification (FR-11.4). The webhook is only a trigger to look up
    /// the refund's authoritative status with Monnify; the claimed outcome in the body is not stored
    /// or trusted (§8), exactly as a transaction notification stores identifiers and re-verifies.
    /// </summary>
    public static InboxMessage ReceiveRefund(
        string refundReference,
        string rawPayload,
        DateTimeOffset receivedAt)
    {
        Guard.AgainstNullOrWhiteSpace(refundReference, nameof(refundReference));

        return new InboxMessage(
            Guid.NewGuid(), WebhookEventType.RefundCompletion,
            transactionReference: null, destinationAccountNumber: null,
            refundReference.Trim(), mandateReference: null, rawPayload, receivedAt);
    }

    /// <summary>
    /// Records a mandate-status-change notification (FR-9.2). As with refunds, the webhook is only a
    /// trigger — the drainer re-verifies the mandate's status with Monnify; the body is not trusted (§8).
    /// </summary>
    public static InboxMessage ReceiveMandate(
        string mandateReference,
        string rawPayload,
        DateTimeOffset receivedAt)
    {
        Guard.AgainstNullOrWhiteSpace(mandateReference, nameof(mandateReference));

        return new InboxMessage(
            Guid.NewGuid(), WebhookEventType.MandateStatusChange,
            transactionReference: null, destinationAccountNumber: null,
            refundReference: null, mandateReference.Trim(), rawPayload, receivedAt);
    }

    public void MarkProcessed(DateTimeOffset at) => ProcessedAt = at;
}
