using FluentAssertions;
using OfrenCollect.Infrastructure.Audit;

namespace OfrenCollect.Infrastructure.UnitTests.Audit;

public class SensitiveDataRedactorTests
{
    [Fact]
    public void Redact_ReplacesPasswordAndSecretFields()
    {
        var result = SensitiveDataRedactor.Redact("{\"email\":\"a@b.ng\",\"password\":\"hunter2\",\"secretKey\":\"sk_live\"}");

        result.Should().NotContain("hunter2");
        result.Should().NotContain("sk_live");
        result.Should().Contain("a@b.ng");
    }

    [Fact]
    public void Redact_MasksAccountNumbersKeepingLastFour()
    {
        var result = SensitiveDataRedactor.Redact("{\"reservedAccountNumber\":\"4003115967\"}");

        result.Should().Contain("5967");
        result.Should().NotContain("4003115967");
        result.Should().Contain("******5967");
    }

    [Fact]
    public void Redact_MasksAccountNumbersInNonJsonText()
    {
        var result = SensitiveDataRedactor.Redact("paid into 4003115967 today");

        result.Should().Contain("******5967");
        result.Should().NotContain("4003115967");
    }

    [Fact]
    public void Redact_RedactsNestedSensitiveFields()
    {
        var result = SensitiveDataRedactor.Redact("{\"outer\":{\"authorization\":\"Bearer xyz\"}}");

        result.Should().NotContain("Bearer xyz");
    }

    [Fact]
    public void Redact_TruncatesVeryLongBodies()
    {
        var result = SensitiveDataRedactor.Redact(new string('x', 5000));

        result!.Length.Should().BeLessThan(5000);
        result.Should().EndWith("[truncated]");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Redact_ReturnsEmptyBodiesUnchanged(string? body)
    {
        SensitiveDataRedactor.Redact(body).Should().Be(body);
    }
}
