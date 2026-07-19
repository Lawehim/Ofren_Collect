using FluentAssertions;
using OfrenCollect.Infrastructure.Auth;

namespace OfrenCollect.Infrastructure.UnitTests.Auth;

public class Pbkdf2PasswordHasherTests
{
    private const string Password = "correct horse battery staple";

    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ThenVerify_WithCorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash(Password);

        _hasher.Verify(Password, hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash(Password);

        _hasher.Verify("wrong password", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_NeverReturnsThePlaintext()
    {
        _hasher.Hash(Password).Should().NotContain(Password);
    }

    [Fact]
    public void Hash_ProducesDifferentHashesForTheSamePassword()
    {
        _hasher.Hash(Password).Should().NotBe(_hasher.Hash(Password));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-hash")]
    [InlineData("1.only-two-parts")]
    public void Verify_WithMalformedHash_ReturnsFalse(string malformedHash)
    {
        _hasher.Verify(Password, malformedHash).Should().BeFalse();
    }
}
