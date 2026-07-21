using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OfrenCollect.Infrastructure.Jobs;

/// <summary>
/// Keeps a Render free instance from spinning down: periodically GETs the service's own public
/// <c>/health</c> endpoint. That request goes out and back through Render's edge, so it counts as
/// inbound traffic and resets the idle timer (a localhost ping would not). Best-effort — a failed
/// ping is logged and retried. No-ops when no public URL is resolved (e.g. locally), so it only runs
/// where it is needed. This keeps an already-running instance warm; the first hit after a fresh
/// deploy can still cold-start, and an external monitor is the fully robust complement.
/// </summary>
public sealed class KeepWarmService : BackgroundService
{
    public const string HttpClientName = "keepwarm";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KeepWarmOptions _options;
    private readonly ILogger<KeepWarmService> _logger;
    private readonly Uri? _healthUrl;

    public KeepWarmService(
        IHttpClientFactory httpClientFactory, KeepWarmOptions options, ILogger<KeepWarmService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;

        // Render injects RENDER_EXTERNAL_URL (the public https URL) automatically.
        var baseUrl = string.IsNullOrWhiteSpace(_options.Url)
            ? Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL")
            : _options.Url;
        _healthUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : new Uri(new Uri(baseUrl), "/health");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || _healthUrl is null)
        {
            return;
        }

        _logger.LogInformation("Keep-warm: pinging {Url} every {Seconds}s.", _healthUrl, _options.IntervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var http = _httpClientFactory.CreateClient(HttpClientName);
                using var response = await http.GetAsync(_healthUrl, stoppingToken);
                _logger.LogDebug("Keep-warm ping {Url} -> {Status}.", _healthUrl, (int)response.StatusCode);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug(exception, "Keep-warm ping failed; will retry next tick.");
            }
        }
    }
}
