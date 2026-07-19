using MediatR;

namespace OfrenCollect.Application.Auth.Login;

/// <summary>Authenticates a user by email and password, returning a signed token (FR-0.2).</summary>
public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResult>;
