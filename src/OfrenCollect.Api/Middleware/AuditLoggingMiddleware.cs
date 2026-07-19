using System.Security.Claims;
using System.Text;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Domain.Audit;
using OfrenCollect.Infrastructure.Audit;
using OfrenCollect.Infrastructure.Auth;

namespace OfrenCollect.Api.Middleware;

/// <summary>
/// Records every API call as an audit entry (FR-8.1): correlation id, user, tenant, method,
/// path, status, duration, and sanitised request/response bodies. Runs outermost so it sees the
/// authenticated principal and the final response (including error responses). Never fails a
/// request — auditing is best-effort.
/// </summary>
public sealed class AuditLoggingMiddleware
{
    public const string CorrelationHeader = "X-Correlation-Id";
    private const int MaxCapturedBytes = 8192;

    private readonly RequestDelegate _next;
    private readonly IAuditLogger _auditLogger;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(
        RequestDelegate next,
        IAuditLogger auditLogger,
        TimeProvider clock,
        ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _auditLogger = auditLogger;
        _clock = clock;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldSkip(context))
        {
            await _next(context);
            return;
        }

        var correlationId = ResolveCorrelationId(context);
        context.Response.Headers[CorrelationHeader] = correlationId;

        var startTimestamp = _clock.GetTimestamp();
        var requestBody = await ReadRequestBodyAsync(context.Request);

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);
        }
        finally
        {
            var responseBody = ReadBounded(buffer);
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
            context.Response.Body = originalBody;

            await RecordAsync(context, correlationId, requestBody, responseBody, startTimestamp);
        }
    }

    private static bool ShouldSkip(HttpContext context)
    {
        var path = context.Request.Path;
        return HttpMethods.IsOptions(context.Request.Method)
            || path.StartsWithSegments("/hubs")
            || path.StartsWithSegments("/health");
    }

    private static string ResolveCorrelationId(HttpContext context) =>
        context.Request.Headers.TryGetValue(CorrelationHeader, out var header) && !string.IsNullOrWhiteSpace(header)
            ? header.ToString()
            : Guid.NewGuid().ToString("N");

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0)
        {
            return null;
        }

        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    private static string? ReadBounded(MemoryStream buffer)
    {
        if (buffer.Length == 0)
        {
            return null;
        }

        var count = (int)Math.Min(buffer.Length, MaxCapturedBytes);
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, count);
    }

    private async Task RecordAsync(
        HttpContext context, string correlationId, string? requestBody, string? responseBody, long startTimestamp)
    {
        try
        {
            var durationMs = (long)_clock.GetElapsedTime(startTimestamp).TotalMilliseconds;
            var entry = AuditEntry.Record(
                tenantId: ParseGuid(context.User.FindFirstValue(JwtTokenService.TenantIdClaim)),
                userId: ParseGuid(context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirstValue("sub")),
                correlationId: correlationId,
                method: context.Request.Method,
                path: context.Request.Path.Value ?? "/",
                queryString: context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
                requestBody: SensitiveDataRedactor.Redact(requestBody),
                responseStatusCode: context.Response.StatusCode,
                responseBody: SensitiveDataRedactor.Redact(responseBody),
                durationMs: durationMs,
                ipAddress: context.Connection.RemoteIpAddress?.ToString(),
                timestampUtc: _clock.GetUtcNow());

            await _auditLogger.LogAsync(entry, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // Auditing must never fail the request; surface the failure to logs only.
            _logger.LogWarning(exception, "Failed to write audit entry for {Path}", context.Request.Path);
        }
    }

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var id) ? id : null;
}
