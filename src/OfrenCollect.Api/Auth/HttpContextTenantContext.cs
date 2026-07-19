using OfrenCollect.Application.Abstractions;
using OfrenCollect.Infrastructure.Auth;

namespace OfrenCollect.Api.Auth;

/// <summary>
/// Resolves the current tenant from the authenticated JWT's <c>tenant_id</c> claim — never from
/// client-supplied input (CLAUDE.md §8, §11.3). Null when there is no authenticated user
/// (anonymous endpoints, the webhook path, and startup seeding).
/// </summary>
public sealed class HttpContextTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextTenantContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public Guid? CurrentTenantId
    {
        get
        {
            var value = _accessor.HttpContext?.User.FindFirst(JwtTokenService.TenantIdClaim)?.Value;
            return Guid.TryParse(value, out var tenantId) ? tenantId : null;
        }
    }
}
