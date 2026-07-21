using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfrenCollect.Application.Mandates.CancelMandate;
using OfrenCollect.Application.Mandates.CreateMandate;
using OfrenCollect.Application.Mandates.DebitMandate;
using OfrenCollect.Application.Mandates.ReconcileMandateDebit;
using OfrenCollect.Application.Mandates.RefreshMandateStatus;
using OfrenCollect.Infrastructure.Mandates;

namespace OfrenCollect.Api.Controllers;

/// <summary>
/// Creates direct-debit mandates (FR-9). Owner-only (§8) and behind the <c>Mandates</c> feature flag
/// — when disabled the endpoint is not found, so the capability stays dark until enabled by
/// configuration (§6). Returns the customer authorization link to send them.
/// </summary>
[ApiController]
[Route("api/mandates")]
[Authorize(Roles = "Owner")]
public sealed class MandatesController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly MandatesOptions _options;

    public MandatesController(ISender mediator, MandatesOptions options)
    {
        _mediator = mediator;
        _options = options;
    }

    [HttpPost]
    public async Task<ActionResult<CreateMandateResult>> Create(CreateMandateCommand command, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return NotFound();
        }

        return Ok(await _mediator.Send(command, ct));
    }

    [HttpPost("{reference}/refresh")]
    public async Task<ActionResult<MandateStatusResult>> Refresh(string reference, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return NotFound();
        }

        return Ok(await _mediator.Send(new RefreshMandateStatusCommand(reference), ct));
    }

    [HttpPost("debit")]
    public async Task<ActionResult<MandateDebitInitiatedResult>> Debit(DebitMandateCommand command, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return NotFound();
        }

        return Ok(await _mediator.Send(command, ct));
    }

    [HttpPost("debit/{paymentReference}/reconcile")]
    public async Task<ActionResult<MandateDebitStatusResult>> ReconcileDebit(string paymentReference, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return NotFound();
        }

        return Ok(await _mediator.Send(new ReconcileMandateDebitCommand(paymentReference), ct));
    }

    [HttpPost("{reference}/cancel")]
    public async Task<IActionResult> Cancel(string reference, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return NotFound();
        }

        await _mediator.Send(new CancelMandateCommand(reference), ct);
        return NoContent();
    }
}
