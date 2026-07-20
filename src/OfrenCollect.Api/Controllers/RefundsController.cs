using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfrenCollect.Application.Refunds;
using OfrenCollect.Application.Refunds.InitiateRefund;
using OfrenCollect.Infrastructure.Refunds;

namespace OfrenCollect.Api.Controllers;

/// <summary>
/// Initiates refunds to customers (FR-11). Owner-only (§8 least privilege) and behind the
/// <c>Refunds</c> feature flag — when disabled the endpoint is not found, so the capability stays
/// dark until enabled by configuration (§6). Every call is audited by the audit middleware.
/// </summary>
[ApiController]
[Route("api/refunds")]
[Authorize(Roles = "Owner")]
public sealed class RefundsController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly RefundsOptions _options;

    public RefundsController(ISender mediator, RefundsOptions options)
    {
        _mediator = mediator;
        _options = options;
    }

    [HttpPost]
    public async Task<ActionResult<RefundResult>> Initiate(InitiateRefundCommand command, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return NotFound();
        }

        return Ok(await _mediator.Send(command, ct));
    }
}
