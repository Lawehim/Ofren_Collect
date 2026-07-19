using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OfrenCollect.Domain.Users;
using OfrenCollect.Infrastructure.Auth;

namespace OfrenCollect.Infrastructure.UnitTests.Auth;

public class JwtTokenServiceTests
{
    private const string SigningKey = "super-secret-signing-key-that-is-definitely-long-enough";
    private const string Issuer = "ofren-collect";
    private const string Audience = "ofren-collect-api";

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static JwtTokenService CreateService() =>
        new(
            new JwtOptions
            {
                Issuer = Issuer,
                Audience = Audience,
                SigningKey = SigningKey,
                AccessTokenMinutes = 60
            },
            TimeProvider.System);

    private static TokenValidationParameters ValidationParameters(string signingKey) => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = false
    };

    [Fact]
    public async Task GenerateToken_ProducesTokenValidWithSameKey_CarryingClaims()
    {
        var token = CreateService().GenerateToken(UserId, TenantId, "ada@brightpath.ng", UserRole.Owner);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, ValidationParameters(SigningKey));

        result.IsValid.Should().BeTrue();
        result.Claims[JwtTokenService.TenantIdClaim].Should().Be(TenantId.ToString());
        result.Claims[JwtTokenService.RoleClaim].Should().Be(nameof(UserRole.Owner));
        result.Claims[JwtRegisteredClaimNames.Sub].Should().Be(UserId.ToString());
        result.Claims[JwtRegisteredClaimNames.Email].Should().Be("ada@brightpath.ng");
    }

    [Fact]
    public async Task GenerateToken_FailsValidationWithWrongKey()
    {
        var token = CreateService().GenerateToken(UserId, TenantId, "ada@brightpath.ng", UserRole.Owner);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            token, ValidationParameters("a-completely-different-but-still-long-enough-key"));

        result.IsValid.Should().BeFalse();
    }
}
