using OfrenCollect.Domain.Users;

namespace OfrenCollect.Application.Abstractions;

/// <summary>
/// Issues signed JWTs carrying the user's identity, tenant, and role (FR-0.2). The signing
/// key lives only in configuration/secrets (NFR-1.8), never in the repo or the frontend.
/// </summary>
public interface IJwtTokenService
{
    string GenerateToken(Guid userId, Guid tenantId, string email, UserRole role);
}
