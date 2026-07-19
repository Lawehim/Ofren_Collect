using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.Webhooks;

/// <summary>
/// A verified webhook notification persisted durably before it is acknowledged (NFR-2.6). The
/// background drainer reconciles unprocessed messages and marks them done, so a crash
/// mid-processing cannot lose an already-acknowledged payment — it is simply re-drained (the
/// reconciliation is idempotent).
/// </summary>
public sealed class InboxMessage : Entity
{
    private InboxMessage()
    {
    }

    private InboxMessage(
        Guid id,
        string transactionReference,
        string destinationAccountNumber,
        string rawPayload,
        DateTimeOffset receivedAt)
        : base(id)
    {
        TransactionReference = transactionReference;
        DestinationAccountNumber = destinationAccountNumber;
        RawPayload = rawPayload;
        ReceivedAt = receivedAt;
    }

    public string TransactionReference { get; private set; } = string.Empty;
    public string DestinationAccountNumber { get; private set; } = string.Empty;
    public string RawPayload { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    public bool IsProcessed => ProcessedAt.HasValue;

    public static InboxMessage Receive(
        string transactionReference,
        string destinationAccountNumber,
        string rawPayload,
        DateTimeOffset receivedAt)
    {
        Guard.AgainstNullOrWhiteSpace(transactionReference, nameof(transactionReference));
        Guard.AgainstNullOrWhiteSpace(destinationAccountNumber, nameof(destinationAccountNumber));

        return new InboxMessage(
            Guid.NewGuid(), transactionReference.Trim(), destinationAccountNumber.Trim(), rawPayload, receivedAt);
    }

    public void MarkProcessed(DateTimeOffset at) => ProcessedAt = at;
}
