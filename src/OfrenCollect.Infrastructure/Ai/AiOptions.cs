namespace OfrenCollect.Infrastructure.Ai;

/// <summary>
/// AI assistant configuration. Disabled by default; when enabled it points at any
/// OpenAI-compatible chat-completions endpoint (hosted, or a free local model like Ollama at
/// http://localhost:11434). The ApiKey is a secret and comes from user-secrets/environment.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public bool Enabled { get; init; }

    public string BaseUrl { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;
}
