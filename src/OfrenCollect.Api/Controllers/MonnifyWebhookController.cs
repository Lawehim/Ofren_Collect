using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Webhooks;
using OfrenCollect.Infrastructure.Monnify;

namespace OfrenCollect.Api.Controllers;

/// <summary>
/// Receives Monnify transaction and refund notifications. Verifies the signature (fail-closed),
/// persists the notification durably, then acknowledges with 200 — the background drainer processes
/// it (FR-3.2, FR-11.4, NFR-2.6). Authenticated by signature, not a JWT: it is called by Monnify.
/// The drainer re-verifies each transaction with Monnify, so the webhook body is never trusted on
/// its own.
/// </summary>
[ApiController]
[Route("api/webhooks/monnify")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public sealed class MonnifyWebhookController : ControllerBase
{
    private const string SignatureHeader = "monnify-signature";
    private const string RefundSucceededEvent = "SUCCESSFUL_REFUND";
    private const string RefundFailedEvent = "FAILED_REFUND";
    private const string MandateUpdateEvent = "MANDATE_UPDATE";

    private readonly IInboxRepository _inbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMonnifyWebhookVerifier _verifier;
    private readonly MonnifyOptions _options;
    private readonly TimeProvider _clock;

    public MonnifyWebhookController(
        IInboxRepository inbox,
        IUnitOfWork unitOfWork,
        IMonnifyWebhookVerifier verifier,
        MonnifyOptions options,
        TimeProvider clock)
    {
        _inbox = inbox;
        _unitOfWork = unitOfWork;
        _verifier = verifier;
        _options = options;
        _clock = clock;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        var signature = Request.Headers[SignatureHeader].FirstOrDefault();
        if (_options.VerifyWebhookSignature && !_verifier.IsValid(rawBody, signature))
        {
            return Unauthorized();
        }

        // Persist durably before acknowledging so a crash cannot lose the notification (NFR-2.6);
        // the drainer processes it. An unrecognisable body is acknowledged and ignored (TC-3.7).
        if (TryBuildMessage(rawBody, _clock.GetUtcNow(), out var message))
        {
            _inbox.Add(message);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Ok();
    }

    private static bool TryBuildMessage(string rawBody, DateTimeOffset receivedAt, [NotNullWhen(true)] out InboxMessage? message)
    {
        message = null;

        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var scope = root.TryGetProperty("eventData", out var eventData) &&
                        eventData.ValueKind == JsonValueKind.Object
                ? eventData
                : root;

            TryReadString(root, "eventType", out var eventType);

            if (string.Equals(eventType, RefundSucceededEvent, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(eventType, RefundFailedEvent, StringComparison.OrdinalIgnoreCase))
            {
                // Refund completion (FR-11.4): store only the reference — the drainer re-verifies the
                // status with Monnify, so the claimed outcome in the body is never trusted (§8). NOTE:
                // the refundReference location in the payload is assumed; confirm against sandbox (§14).
                if (TryReadString(scope, "refundReference", out var refundReference))
                {
                    message = InboxMessage.ReceiveRefund(refundReference, rawBody, receivedAt);
                    return true;
                }

                return false;
            }

            if (string.Equals(eventType, MandateUpdateEvent, StringComparison.OrdinalIgnoreCase))
            {
                // Mandate status change (FR-9.2): our reference is `externalMandateReference`; the
                // drainer re-verifies the status with Monnify (§8).
                if (TryReadString(scope, "externalMandateReference", out var mandateReference))
                {
                    message = InboxMessage.ReceiveMandate(mandateReference, rawBody, receivedAt);
                    return true;
                }

                return false;
            }

            // Transaction completion.
            TryReadString(scope, "transactionReference", out var reference);

            var accountNumber = string.Empty;
            if (scope.TryGetProperty("destinationAccountInformation", out var destination))
            {
                TryReadString(destination, "accountNumber", out accountNumber);
            }
            else
            {
                TryReadString(scope, "destinationAccountNumber", out accountNumber);
            }

            if (reference.Length > 0 && accountNumber.Length > 0)
            {
                message = InboxMessage.Receive(reference, accountNumber, rawBody, receivedAt);
                return true;
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return value.Length > 0;
        }

        return false;
    }
}
