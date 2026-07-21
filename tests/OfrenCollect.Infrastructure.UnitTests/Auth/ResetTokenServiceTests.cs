using FluentAssertions;
using OfrenCollect.Infrastructure.Auth;

namespace OfrenCollect.Infrastructure.UnitTests.Auth;

public class ResetTokenServiceTests
{
    private readonly ResetTokenService _service = new();

    [Fact]
    public void Create_ReturnsRawToken_WithHashMatchingHashMethod()
    {
        var token = _service.Create();

        token.RawToken.Should().NotBeNullOrWhiteSpace();
        token.HashedToken.Should().Be(_service.Hash(token.RawToken));
    }

    [Fact]
    public void Create_ProducesADifferentTokenEachTime()
    {
        _service.Create().RawToken.Should().NotBe(_service.Create().RawToken);
    }

    [Fact]
    public void Hash_IsDeterministic_AndDoesNotReturnTheRawToken()
    {
        const string raw = "some-raw-token";

        var hash = _service.Hash(raw);

        hash.Should().Be(_service.Hash(raw));
        hash.Should().NotBe(raw);
    }
}
