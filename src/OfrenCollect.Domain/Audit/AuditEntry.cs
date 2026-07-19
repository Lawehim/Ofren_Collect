using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.Audit;

/// <summary>
/// A recorded API call (FR-8.1): who, what, when, and the sanitised request/response. Tenant
/// and user are nullable because pre-auth calls (register, login, webhook) have neither.
/// </summary>
public sealed class AuditEntry : Entity
{
    private AuditEntry()
    {
    }

    private AuditEntry(
        Guid id,
        Guid? tenantId,
        Guid? userId,
        string correlationId,
        string method,
        string path,
        string? queryString,
        string? requestBody,
        int responseStatusCode,
        string? responseBody,
        long durationMs,
        string? ipAddress,
        DateTimeOffset timestampUtc)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        CorrelationId = correlationId;
        Method = method;
        Path = path;
        QueryString = queryString;
        RequestBody = requestBody;
        ResponseStatusCode = responseStatusCode;
        ResponseBody = responseBody;
        DurationMs = durationMs;
        IpAddress = ipAddress;
        TimestampUtc = timestampUtc;
    }

    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string Method { get; private set; } = string.Empty;
    public string Path { get; private set; } = string.Empty;
    public string? QueryString { get; private set; }
    public string? RequestBody { get; private set; }
    public int ResponseStatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public long DurationMs { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTimeOffset TimestampUtc { get; private set; }

    public static AuditEntry Record(
        Guid? tenantId,
        Guid? userId,
        string correlationId,
        string method,
        string path,
        string? queryString,
        string? requestBody,
        int responseStatusCode,
        string? responseBody,
        long durationMs,
        string? ipAddress,
        DateTimeOffset timestampUtc)
    {
        Guard.AgainstNullOrWhiteSpace(correlationId, nameof(correlationId));
        Guard.AgainstNullOrWhiteSpace(method, nameof(method));
        Guard.AgainstNullOrWhiteSpace(path, nameof(path));

        return new AuditEntry(
            Guid.NewGuid(), tenantId, userId, correlationId, method, path, queryString, requestBody,
            responseStatusCode, responseBody, durationMs, ipAddress, timestampUtc);
    }
}
