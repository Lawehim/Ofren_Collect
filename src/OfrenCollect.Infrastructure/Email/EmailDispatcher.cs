using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OfrenCollect.Infrastructure.Email;

/// <summary>
/// Drains the email queue and delivers each message via Brevo's transactional HTTP API
/// (POST /v3/smtp/email over port 443, which works where outbound SMTP is blocked). Best-effort: a
/// failure is logged, never thrown, and never affects the request that enqueued the email.
/// </summary>
public sealed class EmailDispatcher : BackgroundService
{
    public const string HttpClientName = "brevo";

    private readonly IEmailOutbox _outbox;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EmailOptions _options;
    private readonly ILogger<EmailDispatcher> _logger;

    public EmailDispatcher(
        IEmailOutbox outbox,
        IHttpClientFactory httpClientFactory,
        EmailOptions options,
        ILogger<EmailDispatcher> logger)
    {
        _outbox = outbox;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var email in _outbox.ReadAllAsync(stoppingToken))
        {
            try
            {
                await SendAsync(email, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Failed to send '{Subject}' to {Recipient}.", email.Subject, email.ToEmail);
            }
        }
    }

    private async Task SendAsync(QueuedEmail email, CancellationToken cancellationToken)
    {
        var http = _httpClientFactory.CreateClient(HttpClientName);

        var payload = new BrevoEmail(
            new BrevoSender(_options.FromName, _options.FromAddress),
            [new BrevoRecipient(email.ToEmail)],
            email.Subject,
            email.HtmlBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v3/smtp/email")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("api-key", _options.ApiKey);

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Sent '{Subject}' to {Recipient}.", email.Subject, email.ToEmail);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "Brevo rejected '{Subject}' to {Recipient}: {Status} {Body}",
            email.Subject, email.ToEmail, (int)response.StatusCode, body);
    }

    private sealed record BrevoEmail(
        [property: JsonPropertyName("sender")] BrevoSender Sender,
        [property: JsonPropertyName("to")] IReadOnlyList<BrevoRecipient> To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("htmlContent")] string HtmlContent);

    private sealed record BrevoSender(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("email")] string Email);

    private sealed record BrevoRecipient(
        [property: JsonPropertyName("email")] string Email);
}
