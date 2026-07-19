using System.Globalization;
using System.Net;
using System.Text;
using FluentAssertions;
using OfrenCollect.Infrastructure.Monnify;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Infrastructure.UnitTests.Monnify;

public class MonnifyClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    private const string AuthJson =
        """{"requestSuccessful":true,"responseMessage":"ok","responseCode":"0","responseBody":{"accessToken":"tok","expiresIn":3599}}""";

    private static MonnifyClient CreateClient(string verifyJson)
    {
        var http = new HttpClient(new StubHandler(AuthJson, verifyJson))
        {
            BaseAddress = new Uri("https://sandbox.monnify.com")
        };
        var options = new MonnifyOptions { ApiKey = "key", SecretKey = "secret" };
        return new MonnifyClient(http, options, new FixedClock(Now));
    }

    private static string VerifyJson(string status, decimal amountPaid, string paidOn = "17/07/2026 09:13:00 AM") =>
        "{\"requestSuccessful\":true,\"responseMessage\":\"ok\",\"responseCode\":\"0\",\"responseBody\":{"
        + "\"transactionReference\":\"MNFY|123\",\"amountPaid\":" + amountPaid.ToString(CultureInfo.InvariantCulture)
        + ",\"paymentStatus\":\"" + status + "\",\"paidOn\":\"" + paidOn + "\","
        + "\"accountDetails\":{\"accountNumber\":\"7080124933\"}}}";

    [Fact]
    public async Task VerifyTransaction_WhenPaid_MapsAmountAccountReferenceAndSuccess()
    {
        var client = CreateClient(VerifyJson("PAID", 5000.00m));

        var result = await client.VerifyTransactionAsync("MNFY|123", CancellationToken.None);

        result.IsSuccessful.Should().BeTrue();
        result.Amount.Should().Be(Money.Of(5000m));
        result.DestinationAccountNumber.Should().Be("7080124933");
        result.TransactionReference.Should().Be("MNFY|123");
        result.PaidAt.Should().Be(new DateTimeOffset(2026, 7, 17, 9, 13, 0, TimeSpan.FromHours(1)));
    }

    [Fact]
    public async Task VerifyTransaction_WhenPartiallyPaid_IsSuccessful_WithPartialAmount()
    {
        var client = CreateClient(VerifyJson("PARTIALLY_PAID", 3000m));

        var result = await client.VerifyTransactionAsync("MNFY|123", CancellationToken.None);

        result.IsSuccessful.Should().BeTrue();
        result.Amount.Should().Be(Money.Of(3000m));
    }

    [Fact]
    public async Task VerifyTransaction_WhenFailed_IsNotSuccessful()
    {
        var client = CreateClient(VerifyJson("FAILED", 0m));

        var result = await client.VerifyTransactionAsync("MNFY|123", CancellationToken.None);

        result.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyTransaction_WhenPaidOnUnparseable_FallsBackToClock()
    {
        var client = CreateClient(VerifyJson("PAID", 5000m, paidOn: "not-a-date"));

        var result = await client.VerifyTransactionAsync("MNFY|123", CancellationToken.None);

        result.PaidAt.Should().Be(Now);
    }

    [Fact]
    public async Task VerifyTransaction_WhenEnvelopeUnsuccessful_ThrowsMonnifyException()
    {
        var client = CreateClient(
            """{"requestSuccessful":false,"responseMessage":"boom","responseCode":"99","responseBody":null}""");

        var act = async () => await client.VerifyTransactionAsync("MNFY|123", CancellationToken.None);

        await act.Should().ThrowAsync<MonnifyException>();
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _authJson;
        private readonly string _verifyJson;

        public StubHandler(string authJson, string verifyJson)
        {
            _authJson = authJson;
            _verifyJson = verifyJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = request.RequestUri!.AbsolutePath.Contains("/auth/login", StringComparison.Ordinal)
                ? _authJson
                : _verifyJson;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
