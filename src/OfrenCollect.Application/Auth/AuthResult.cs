namespace OfrenCollect.Application.Auth;

/// <summary>The outcome of a successful registration or login: a signed token and who it is for.</summary>
public sealed record AuthResult(string Token, string Email, string Role);
