using MediatR;

namespace OfrenCollect.Application.Auth.ResetPassword;

/// <summary>Completes a password reset using a token from the emailed link.</summary>
public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest;
