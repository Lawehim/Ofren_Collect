using OfrenCollect.Application.Assistant;

namespace OfrenCollect.Application.Abstractions;

/// <summary>
/// Answers a plain-language question about the current tenant's own collections (FR-7.1).
/// Read-only and grounded: it never creates, modifies, cancels, or moves money, and never
/// returns a figure not computed from the tenant's real data. Behind a feature flag — a
/// no-op implementation is registered when disabled (FR-7.4).
/// </summary>
public interface IAiAssistant
{
    Task<AssistantAnswer> AskAsync(string question, CancellationToken cancellationToken);
}
