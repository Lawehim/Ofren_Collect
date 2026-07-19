using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Domain.Users;

namespace OfrenCollect.Infrastructure.Auth;

/// <summary>
/// Issues HMAC-SHA256 signed JWTs carrying <c>sub</c>, <c>email</c>, <c>tenant_id</c>, and
/// <c>role</c>. The tenant claim is what every authenticated request derives its tenant from.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    public const string TenantIdClaim = "tenant_id";
    public const string RoleClaim = "role";

    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;

    public JwtTokenService(JwtOptions options, TimeProvider clock)
    {
        _options = options;
        _clock = clock;
    }

    public string GenerateToken(Guid userId, Guid tenantId, string email, UserRole role)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var issuedAt = _clock.GetUtcNow().UtcDateTime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt.AddMinutes(_options.AccessTokenMinutes),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                [JwtRegisteredClaimNames.Email] = email,
                [TenantIdClaim] = tenantId.ToString(),
                [RoleClaim] = role.ToString()
            },
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
