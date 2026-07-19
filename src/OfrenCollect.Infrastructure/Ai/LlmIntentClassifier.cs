using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Assistant;

namespace OfrenCollect.Infrastructure.Ai;

/// <summary>
/// Classifies a question into one <see cref="CollectionsIntent"/> using any OpenAI-compatible
/// chat-completions endpoint. The model is constrained to reply with a single intent keyword and
/// never sees data; anything it can't classify (or any error) is treated as Unknown — fail-safe.
/// </summary>
public sealed class LlmIntentClassifier : IIntentClassifier
{
    private const string SystemPrompt =
        "You classify a small-business owner's question about their payment collections into "
        + "exactly ONE of these intents, replying with only the intent keyword and nothing else:\n"
        + "- collected_this_week: how much money has been collected this week\n"
        + "- overdue_customers: which/how many subscriptions are overdue\n"
        + "- underpaid_customers: which/how many invoices are underpaid\n"
        + "- active_subscriptions: how many active subscriptions there are\n"
        + "- unmatched_payments: how many payments are unmatched\n"
        + "- unknown: anything else, OR any request to create, change, cancel, refund, or move "
        + "money (you are read-only and must never act).\n"
        + "Reply with one keyword only.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly AiOptions _options;

    public LlmIntentClassifier(HttpClient http, AiOptions options)
    {
        _http = http;
        _options = options;
    }

    public async Task<CollectionsIntent> ClassifyAsync(string question, CancellationToken cancellationToken)
    {
        var request = new ChatRequest(
            _options.Model,
            [new ChatMessage("system", SystemPrompt), new ChatMessage("user", question)],
            Temperature: 0,
            MaxTokens: 16);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        using var response = await _http.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return CollectionsIntent.Unknown;
        }

        var body = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions, cancellationToken);
        var content = body?.Choices is { Count: > 0 } choices
            ? choices[0].Message?.Content ?? string.Empty
            : string.Empty;
        return MapToIntent(content);
    }

    private static CollectionsIntent MapToIntent(string content)
    {
        var text = content.ToLowerInvariant();
        if (text.Contains("collected_this_week", StringComparison.Ordinal)) return CollectionsIntent.CollectedThisWeek;
        if (text.Contains("overdue", StringComparison.Ordinal)) return CollectionsIntent.OverdueCustomers;
        if (text.Contains("underpaid", StringComparison.Ordinal)) return CollectionsIntent.UnderpaidCustomers;
        if (text.Contains("active_subscriptions", StringComparison.Ordinal)) return CollectionsIntent.ActiveSubscriptions;
        if (text.Contains("unmatched", StringComparison.Ordinal)) return CollectionsIntent.UnmatchedPayments;
        return CollectionsIntent.Unknown;
    }

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatMessage? Message);
}
