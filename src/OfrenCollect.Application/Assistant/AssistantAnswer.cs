namespace OfrenCollect.Application.Assistant;

/// <summary>
/// The assistant's reply. <see cref="Grounded"/> is true only when the answer is computed from
/// the tenant's real data; a declined or unavailable answer is not grounded.
/// </summary>
public sealed record AssistantAnswer(string Answer, bool Grounded, string Intent);
