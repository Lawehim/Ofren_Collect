using OfrenCollect.Domain.Webhooks;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Durable store for verified webhook notifications awaiting processing (NFR-2.6).</summary>
public interface IInboxRepository
{
    void Add(InboxMessage message);

    /// <summary>The oldest unprocessed messages, tracked so they can be marked processed.</summary>
    Task<IReadOnlyList<InboxMessage>> GetUnprocessedAsync(int limit, CancellationToken cancellationToken);
}
