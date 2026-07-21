using System.Threading.Channels;

namespace OfrenCollect.Infrastructure.Email;

/// <summary>
/// A bounded in-memory email outbox. Enqueue is a non-blocking <c>TryWrite</c>; if the outbox is
/// ever saturated, the oldest entry is dropped (email is best-effort, never worth blocking a request).
/// </summary>
public sealed class ChannelEmailOutbox : IEmailOutbox
{
    private const int Capacity = 500;

    private readonly Channel<QueuedEmail> _channel = Channel.CreateBounded<QueuedEmail>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public void Enqueue(QueuedEmail email) => _channel.Writer.TryWrite(email);

    public IAsyncEnumerable<QueuedEmail> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
