using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfrenCollect.Application.Subscriptions;
using OfrenCollect.Application.Subscriptions.EnrolCustomer;

namespace OfrenCollect.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly ISender _mediator;

    public SubscriptionsController(ISender mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<ActionResult<SubscriptionResponse>> Enrol(EnrolCustomerCommand command, CancellationToken ct) =>
        Ok(await _mediator.Send(command, ct));
}
