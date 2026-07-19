using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OfrenCollect.Application.Auth;
using OfrenCollect.Application.Auth.Login;
using OfrenCollect.Application.Auth.Register;

namespace OfrenCollect.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _mediator;

    public AuthController(ISender mediator) => _mediator = mediator;

    [HttpPost("register")]
    public async Task<ActionResult<AuthResult>> Register(RegisterBusinessCommand command, CancellationToken ct) =>
        Ok(await _mediator.Send(command, ct));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResult>> Login(LoginCommand command, CancellationToken ct) =>
        Ok(await _mediator.Send(command, ct));
}
