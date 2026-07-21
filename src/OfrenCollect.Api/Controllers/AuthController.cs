using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OfrenCollect.Application.Auth;
using OfrenCollect.Application.Auth.ForgotPassword;
using OfrenCollect.Application.Auth.Login;
using OfrenCollect.Application.Auth.Register;
using OfrenCollect.Application.Auth.ResetPassword;

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

    // Always returns the same response whether or not the email exists (anti-enumeration).
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(RequestPasswordResetCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return Ok(new { message = "If that email has an account, a reset link is on its way." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return Ok(new { message = "Your password has been reset. You can sign in now." });
    }
}
