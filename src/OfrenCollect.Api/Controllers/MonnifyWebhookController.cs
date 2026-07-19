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
/// Receives Monnify transaction notifications. Verifies the signature (fail-closed), persists the
/// notification durably, then acknowledges with 200 — the background drainer reconciles it
/// (FR-3.2, NFR-2.6). Authenticated by signature, not a JWT: it is called by Monnify. The drainer
/// re-verifies each transaction with Monnify, so the webhook body is never trusted on its own.
/// </summary>
[ApiController]
[Route("api/webhooks/monnify")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public sealed class MonnifyWebhookController : ControllerBase
{
    private const string SignatureHeader = "monnify-signature";

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

        // Persist durably before acknowledging so a crash cannot lose the payment (NFR-2.6);
        // the drainer reconciles it. An unrecognisable body is acknowledged and ignored (TC-3.7).
        if (TryExtract(rawBody, out var reference, out var accountNumber))
        {
            _inbox.Add(InboxMessage.Receive(reference, accountNumber, rawBody, _clock.GetUtcNow()));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Ok();
    }

    private static bool TryExtract(string rawBody, out string reference, out string accountNumber)
    {
        reference = string.Empty;
        accountNumber = string.Empty;

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

            TryReadString(scope, "transactionReference", out reference);

            if (scope.TryGetProperty("destinationAccountInformation", out var destination))
            {
                TryReadString(destination, "accountNumber", out accountNumber);
            }
            else
            {
                TryReadString(scope, "destinationAccountNumber", out accountNumber);
            }

            return reference.Length > 0 && accountNumber.Length > 0;
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
