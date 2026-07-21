namespace OfrenCollect.Application.Auth;

/// <summary>Raised when a password-reset token is unknown, already used, or expired.</summary>
public sealed class InvalidResetTokenException : Exception
{
    public InvalidResetTokenException()
        : base("The password reset link is invalid or has expired.")
    {
    }
}
