using System.Globalization;
using System.Net;
using System.Text;
using FluentAssertions;
using OfrenCollect.Application.Abstractions;
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
    public async Task CreateReservedAccount_MapsFirstAccountFromResponse()
    {
        var reservedJson =
            "{\"requestSuccessful\":true,\"responseMessage\":\"ok\",\"responseCode\":\"0\",\"responseBody\":{"
            + "\"accountReference\":\"OFREN-1\",\"accounts\":["
            + "{\"accountNumber\":\"7080124933\",\"bankName\":\"Wema Bank\"},"
            + "{\"accountNumber\":\"5050505050\",\"bankName\":\"Sterling Bank\"}]}}";
        var client = CreateClient(reservedJson);

        var account = await client.CreateReservedAccountAsync(
            new OfrenCollect.Application.Abstractions.CreateReservedAccountRequest(
                "OFREN-1", "Chidi Eze", "chidi@mail.com"),
            CancellationToken.None);

        account.AccountNumber.Should().Be("7080124933");
        account.BankName.Should().Be("Wema Bank");
    }

    [Fact]
    public async Task VerifyTransaction_WhenHttpError_ThrowsMonnifyException()
    {
        var http = new HttpClient(new StubHandler(AuthJson, "{}", HttpStatusCode.InternalServerError))
        {
            BaseAddress = new Uri("https://sandbox.monnify.com")
        };
        var client = new MonnifyClient(http, new MonnifyOptions { ApiKey = "key", SecretKey = "secret" }, new FixedClock(Now));

        var act = async () => await client.VerifyTransactionAsync("MNFY|123", CancellationToken.None);

        await act.Should().ThrowAsync<MonnifyException>();
    }

    [Fact]
    public async Task VerifyTransaction_WhenEnvelopeUnsuccessful_ThrowsMonnifyException()
    {
        var client = CreateClient(
            """{"requestSuccessful":false,"responseMessage":"boom","responseCode":"99","responseBody":null}""");

        var act = async () => await client.VerifyTransactionAsync("MNFY|123", CancellationToken.None);

        await act.Should().ThrowAsync<MonnifyException>();
    }

    private static string RefundJson(string refundStatus) =>
        "{\"requestSuccessful\":true,\"responseMessage\":\"ok\",\"responseCode\":\"0\",\"responseBody\":{"
        + "\"refundStatus\":\"" + refundStatus + "\"}}";

    [Theory]
    [InlineData("COMPLETED", MonnifyRefundStatus.Completed)]
    [InlineData("FAILED", MonnifyRefundStatus.Failed)]
    [InlineData("PENDING", MonnifyRefundStatus.Pending)]
    [InlineData("weird", MonnifyRefundStatus.Pending)]
    public async Task InitiateRefund_MapsRefundStatus(string status, MonnifyRefundStatus expected)
    {
        var client = CreateClient(RefundJson(status));

        var result = await client.InitiateRefundAsync(
            new OfrenCollect.Application.Abstractions.RefundInitiationRequest(
                "MNFY|123", "RF-1", Money.Of(1000m), "reason", "note"),
            CancellationToken.None);

        result.Status.Should().Be(expected);
    }

    [Fact]
    public async Task GetRefundStatus_MapsFromResponse()
    {
        var client = CreateClient(RefundJson("COMPLETED"));

        var status = await client.GetRefundStatusAsync("RF-1", CancellationToken.None);

        status.Should().Be(MonnifyRefundStatus.Completed);
    }

    [Fact]
    public async Task GetRefundStatus_WhenHttpError_ThrowsMonnifyException()
    {
        var http = new HttpClient(new StubHandler(AuthJson, "{}", HttpStatusCode.NotFound))
        {
            BaseAddress = new Uri("https://sandbox.monnify.com")
        };
        var client = new MonnifyClient(http, new MonnifyOptions { ApiKey = "key", SecretKey = "secret" }, new FixedClock(Now));

        var act = async () => await client.GetRefundStatusAsync("RF-1", CancellationToken.None);

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
        private readonly string _mainJson;
        private readonly HttpStatusCode _mainStatus;

        public StubHandler(string authJson, string mainJson, HttpStatusCode mainStatus = HttpStatusCode.OK)
        {
            _authJson = authJson;
            _mainJson = mainJson;
            _mainStatus = mainStatus;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var isAuth = request.RequestUri!.AbsolutePath.Contains("/auth/login", StringComparison.Ordinal);
            var response = new HttpResponseMessage(isAuth ? HttpStatusCode.OK : _mainStatus)
            {
                Content = new StringContent(isAuth ? _authJson : _mainJson, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
