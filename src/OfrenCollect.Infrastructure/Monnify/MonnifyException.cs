namespace OfrenCollect.Infrastructure.Monnify;

/// <summary>Raised when a Monnify call fails or returns an unusable response.</summary>
public sealed class MonnifyException : Exception
{
    public MonnifyException(string message) : base(message)
    {
    }

    public MonnifyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
