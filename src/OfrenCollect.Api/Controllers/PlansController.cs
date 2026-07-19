using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfrenCollect.Application.Plans;
using OfrenCollect.Application.Plans.CreatePlan;
using OfrenCollect.Application.Plans.ListPlans;

namespace OfrenCollect.Api.Controllers;

[ApiController]
[Route("api/plans")]
[Authorize]
public sealed class PlansController : ControllerBase
{
    private readonly ISender _mediator;

    public PlansController(ISender mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<ActionResult<PlanResponse>> Create(CreatePlanCommand command, CancellationToken ct) =>
        Ok(await _mediator.Send(command, ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlanResponse>>> List(CancellationToken ct) =>
        Ok(await _mediator.Send(new ListPlansQuery(), ct));
}
