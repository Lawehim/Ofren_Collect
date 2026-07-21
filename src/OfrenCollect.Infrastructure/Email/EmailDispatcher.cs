using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OfrenCollect.Infrastructure.Email;

/// <summary>
/// Drains the email outbox and delivers each message via the configured provider's HTTP API (port
/// 443, so it works where outbound SMTP is blocked, e.g. Render). Best-effort: a failure is logged,
/// never thrown, and never affects the request that enqueued the email.
/// </summary>
public sealed class EmailDispatcher : BackgroundService
{
    public const string HttpClientName = "email";

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
        using var request = BuildRequest(email);

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation(
                "Sent '{Subject}' to {Recipient} via {Provider}.", email.Subject, email.ToEmail, _options.Provider);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "{Provider} rejected '{Subject}' to {Recipient}: {Status} {Body}",
            _options.Provider, email.Subject, email.ToEmail, (int)response.StatusCode, body);
    }

    private HttpRequestMessage BuildRequest(QueuedEmail email)
    {
        if (_options.Provider == EmailProvider.Mailtrap)
        {
            // Sandbox: emails are captured in the inbox, not delivered. URL carries the inbox id.
            var payload = new MailtrapEmail(
                new MailtrapAddress(_options.FromAddress, _options.FromName),
                [new MailtrapAddress(email.ToEmail, null)],
                email.Subject,
                email.HtmlBody);
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/send/{_options.MailtrapInboxId}")
            {
                Content = JsonContent.Create(payload),
            };
            request.Headers.Add("Api-Token", _options.ApiKey);
            return request;
        }

        var brevo = new BrevoEmail(
            new BrevoSender(_options.FromName, _options.FromAddress),
            [new BrevoRecipient(email.ToEmail)],
            email.Subject,
            email.HtmlBody);
        var brevoRequest = new HttpRequestMessage(HttpMethod.Post, "/v3/smtp/email")
        {
            Content = JsonContent.Create(brevo),
        };
        brevoRequest.Headers.Add("api-key", _options.ApiKey);
        return brevoRequest;
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

    private sealed record MailtrapEmail(
        [property: JsonPropertyName("from")] MailtrapAddress From,
        [property: JsonPropertyName("to")] IReadOnlyList<MailtrapAddress> To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html);

    private sealed record MailtrapAddress(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name")] string? Name);
}
