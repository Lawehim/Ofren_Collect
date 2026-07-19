using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Assistant;

namespace OfrenCollect.Infrastructure.Ai;

/// <summary>
/// Registered when the assistant is disabled (FR-7.4). Behaves like the real one — same
/// interface — but is switched off, so the rest of the app compiles and runs identically.
/// </summary>
public sealed class NullAiAssistant : IAiAssistant
{
    public Task<AssistantAnswer> AskAsync(string question, CancellationToken cancellationToken) =>
        Task.FromResult(new AssistantAnswer(
            "The assistant is currently unavailable.", Grounded: false, Intent: "Unavailable"));
}
