using MediatR;

namespace OfrenCollect.Application.Auth.ForgotPassword;

/// <summary>Starts a password reset for the given email. Always succeeds — it never reveals whether
/// the email is registered (anti-enumeration).</summary>
public sealed record RequestPasswordResetCommand(string Email) : IRequest;
