using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfrenCollect.Application.Dashboard;
using OfrenCollect.Application.Dashboard.GetDashboard;

namespace OfrenCollect.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly ISender _mediator;

    public DashboardController(ISender mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetDashboardQuery(), ct));
}
