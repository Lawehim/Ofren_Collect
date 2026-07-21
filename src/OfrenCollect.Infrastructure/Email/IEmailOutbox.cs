namespace OfrenCollect.Infrastructure.Email;

/// <summary>
/// An in-process outbox of emails to send. Enqueuing is instant and non-blocking, so a request never
/// waits on the email provider; a background dispatcher drains the outbox and sends (best-effort).
/// </summary>
public interface IEmailOutbox
{
    void Enqueue(QueuedEmail email);

    IAsyncEnumerable<QueuedEmail> ReadAllAsync(CancellationToken cancellationToken);
}

/// <summary>A rendered email awaiting delivery.</summary>
public sealed record QueuedEmail(string ToEmail, string Subject, string HtmlBody);
