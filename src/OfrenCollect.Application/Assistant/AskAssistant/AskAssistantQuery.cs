using MediatR;

namespace OfrenCollect.Application.Assistant.AskAssistant;

/// <summary>Asks the assistant a natural-language question about the current tenant's collections.</summary>
public sealed record AskAssistantQuery(string Question) : IRequest<AssistantAnswer>;
