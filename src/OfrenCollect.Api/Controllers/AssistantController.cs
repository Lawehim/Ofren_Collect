using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfrenCollect.Application.Assistant;
using OfrenCollect.Application.Assistant.AskAssistant;

namespace OfrenCollect.Api.Controllers;

[ApiController]
[Route("api/assistant")]
[Authorize]
public sealed class AssistantController : ControllerBase
{
    private readonly ISender _mediator;

    public AssistantController(ISender mediator) => _mediator = mediator;

    [HttpPost("ask")]
    public async Task<ActionResult<AssistantAnswer>> Ask(AskAssistantQuery query, CancellationToken ct) =>
        Ok(await _mediator.Send(query, ct));
}
