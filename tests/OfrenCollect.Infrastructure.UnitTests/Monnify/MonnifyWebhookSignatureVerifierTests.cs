using FluentAssertions;
using OfrenCollect.Infrastructure.Monnify;

namespace OfrenCollect.Infrastructure.UnitTests.Monnify;

public class MonnifyWebhookSignatureVerifierTests
{
    private const string SecretKey = "test-secret-key";
    private const string Body = "{\"amount\":5000}";

    // HMAC-SHA512(Body, SecretKey) as lowercase hex — computed independently with openssl.
    private const string ValidSignature =
        "2ad80406727c539a5213590e04e75b7d1748584a40b4527f79df22df301587814ae2b304ade214b9a3a05b4ca899377df013f5fe04c2c64273c523462359889b";

    private static MonnifyWebhookSignatureVerifier CreateVerifier() =>
        new(new MonnifyOptions { SecretKey = SecretKey });

    [Fact]
    public void IsValid_WithCorrectSignature_ReturnsTrue()
    {
        CreateVerifier().IsValid(Body, ValidSignature).Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithTamperedBody_ReturnsFalse()
    {
        CreateVerifier().IsValid("{\"amount\":6000}", ValidSignature).Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithWrongSignature_ReturnsFalse()
    {
        CreateVerifier().IsValid(Body, new string('a', 128)).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithMissingSignature_FailsClosed(string? signature)
    {
        CreateVerifier().IsValid(Body, signature).Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithSignatureOfDifferentLength_ReturnsFalse()
    {
        CreateVerifier().IsValid(Body, "abcd").Should().BeFalse();
    }
}
