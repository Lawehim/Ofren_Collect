namespace OfrenCollect.Application.Auth;

/// <summary>Raised when registering with an email that already has an account.</summary>
public sealed class EmailAlreadyInUseException : Exception
{
    public EmailAlreadyInUseException() : base("An account with this email already exists.")
    {
    }
}
