using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;

namespace OfrenCollect.Application.Audit.GetAuditLog;

public sealed class GetAuditLogQueryHandler : IRequestHandler<GetAuditLogQuery, IReadOnlyList<AuditEntryResponse>>
{
    private const int DefaultLimit = 100;

    private readonly IAuditReader _auditReader;
    private readonly ITenantContext _tenantContext;

    public GetAuditLogQueryHandler(IAuditReader auditReader, ITenantContext tenantContext)
    {
        _auditReader = auditReader;
        _tenantContext = tenantContext;
    }

    public Task<IReadOnlyList<AuditEntryResponse>> Handle(
        GetAuditLogQuery query, CancellationToken cancellationToken) =>
        _auditReader.GetForTenantAsync(_tenantContext.RequireTenantId(), DefaultLimit, cancellationToken);
}
