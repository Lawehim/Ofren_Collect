using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Reconciliation.HandleTransactionNotification;
using OfrenCollect.Infrastructure.Monnify;

namespace OfrenCollect.Api.Controllers;

/// <summary>
/// Receives Monnify transaction notifications. Verifies the signature (fail-closed), then
/// acknowledges with 200 and reconciles. Authenticated by signature, not a JWT — it is called
/// by Monnify, not a logged-in user (§11.3). Reconciliation re-verifies with Monnify, so the
/// webhook body is never trusted on its own.
/// </summary>
[ApiController]
[Route("api/webhooks/monnify")]
[AllowAnonymous]
public sealed class MonnifyWebhookController : ControllerBase
{
    private const string SignatureHeader = "monnify-signature";

    private readonly ISender _mediator;
    private readonly IMonnifyWebhookVerifier _verifier;
    private readonly MonnifyOptions _options;

    public MonnifyWebhookController(ISender mediator, IMonnifyWebhookVerifier verifier, MonnifyOptions options)
    {
        _mediator = mediator;
        _verifier = verifier;
        _options = options;
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

        var reference = ExtractTransactionReference(rawBody);
        if (reference is not null)
        {
            await _mediator.Send(new HandleTransactionNotificationCommand(reference), cancellationToken);
        }

        // Acknowledge regardless — an unrecognisable body must not trigger endless retries (TC-3.7).
        return Ok();
    }

    private static string? ExtractTransactionReference(string rawBody)
    {
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;

            if (TryReadString(root, "transactionReference", out var direct))
            {
                return direct;
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("eventData", out var eventData) &&
                TryReadString(eventData, "transactionReference", out var nested))
            {
                return nested;
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
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
