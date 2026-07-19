using MediatR;

namespace OfrenCollect.Application.Auth.Register;

/// <summary>Self-registers a business: creates a tenant and its owner user (FR-0.1).</summary>
public sealed record RegisterBusinessCommand(string BusinessName, string Email, string Password)
    : IRequest<AuthResult>;
