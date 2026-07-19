namespace OfrenCollect.Application.Common;

/// <summary>Raised when a referenced entity does not exist within the current tenant's scope.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}
