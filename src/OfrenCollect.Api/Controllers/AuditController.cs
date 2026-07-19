using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfrenCollect.Application.Audit;
using OfrenCollect.Application.Audit.GetAuditLog;

namespace OfrenCollect.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Roles = "Owner")]
public sealed class AuditController : ControllerBase
{
    private readonly ISender _mediator;

    public AuditController(ISender mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditEntryResponse>>> Get(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetAuditLogQuery(), ct));
}
