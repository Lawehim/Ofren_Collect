using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OfrenCollect.Api.Controllers;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Webhooks;
using OfrenCollect.Infrastructure.Monnify;

namespace OfrenCollect.Api.IntegrationTests.Webhooks;

// Plain unit tests of the webhook controller's parsing/branching — no database, so no Postgres
// fixture. Covers the transaction vs refund vs unrecognised body branches (FR-3.2, FR-11.4).
public class MonnifyWebhookControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IInboxRepository _inbox = Substitute.For<IInboxRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMonnifyWebhookVerifier _verifier = Substitute.For<IMonnifyWebhookVerifier>();

    private async Task<IActionResult> Post(string body, bool verifySignature = false, string? signature = "sig")
    {
        var options = new MonnifyOptions { VerifyWebhookSignature = verifySignature };
        var controller = new MonnifyWebhookController(_inbox, _unitOfWork, _verifier, options, new FixedClock(Now));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (signature is not null)
        {
            httpContext.Request.Headers["monnify-signature"] = signature;
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return await controller.Receive(CancellationToken.None);
    }

    [Fact]
    public async Task Receive_TransactionCompletion_StoresTransactionInboxMessage()
    {
        const string body =
            "{\"eventType\":\"SUCCESSFUL_TRANSACTION\",\"eventData\":{\"transactionReference\":\"MNFY-1\","
            + "\"destinationAccountInformation\":{\"accountNumber\":\"7080000001\"}}}";

        var result = await Post(body);

        result.Should().BeOfType<OkResult>();
        _inbox.Received(1).Add(Arg.Is<InboxMessage>(m =>
            m != null && m.EventType == WebhookEventType.TransactionCompletion && m.TransactionReference == "MNFY-1"));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Receive_SuccessfulRefund_StoresRefundInboxMessage()
    {
        const string body =
            "{\"eventType\":\"SUCCESSFUL_REFUND\",\"eventData\":{\"refundReference\":\"OFREN-RF-1\"}}";

        var result = await Post(body);

        result.Should().BeOfType<OkResult>();
        _inbox.Received(1).Add(Arg.Is<InboxMessage>(m =>
            m != null && m.EventType == WebhookEventType.RefundCompletion && m.RefundReference == "OFREN-RF-1"));
    }

    [Fact]
    public async Task Receive_UnrecognisableBody_IsAcknowledged_ButStoresNothing()
    {
        var result = await Post("not json at all");

        result.Should().BeOfType<OkResult>();
        _inbox.DidNotReceive().Add(Arg.Any<InboxMessage>());
    }

    [Fact]
    public async Task Receive_RefundEventWithoutReference_StoresNothing()
    {
        var result = await Post("{\"eventType\":\"SUCCESSFUL_REFUND\",\"eventData\":{}}");

        result.Should().BeOfType<OkResult>();
        _inbox.DidNotReceive().Add(Arg.Any<InboxMessage>());
    }

    [Fact]
    public async Task Receive_WhenSignatureInvalid_ReturnsUnauthorized()
    {
        _verifier.IsValid(Arg.Any<string>(), Arg.Any<string?>()).Returns(false);

        var result = await Post("{\"eventType\":\"SUCCESSFUL_TRANSACTION\"}", verifySignature: true);

        result.Should().BeOfType<UnauthorizedResult>();
        _inbox.DidNotReceive().Add(Arg.Any<InboxMessage>());
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
