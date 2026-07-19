namespace OfrenCollect.Application.Auth;

/// <summary>
/// Raised when login credentials are wrong. The message is deliberately generic so it never
/// reveals which field was wrong (TC-0.4).
/// </summary>
public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Email or password is incorrect.")
    {
    }
}
