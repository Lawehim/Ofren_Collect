using MediatR;
using OfrenCollect.Application.Abstractions;

namespace OfrenCollect.Application.Assistant.AskAssistant;

public sealed class AskAssistantQueryHandler : IRequestHandler<AskAssistantQuery, AssistantAnswer>
{
    private readonly IAiAssistant _assistant;

    public AskAssistantQueryHandler(IAiAssistant assistant) => _assistant = assistant;

    public Task<AssistantAnswer> Handle(AskAssistantQuery query, CancellationToken cancellationToken) =>
        _assistant.AskAsync(query.Question, cancellationToken);
}
