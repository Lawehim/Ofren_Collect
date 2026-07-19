using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Webhooks;

namespace OfrenCollect.Repository.Persistence.Repositories;

/// <inheritdoc />
public sealed class InboxRepository : IInboxRepository
{
    private readonly OfrenDbContext _db;

    public InboxRepository(OfrenDbContext db) => _db = db;

    public void Add(InboxMessage message) => _db.InboxMessages.Add(message);

    public async Task<IReadOnlyList<InboxMessage>> GetUnprocessedAsync(
        int limit, CancellationToken cancellationToken) =>
        // Tracked (not AsNoTracking): the drainer marks these processed and saves.
        await _db.InboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.ReceivedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
