namespace OfrenCollect.SharedKernel;

/// <summary>
/// Currencies the <see cref="Money"/> value object can represent. The Ofren Collect
/// application deals only in <see cref="Ngn"/>; other members exist so the value object
/// can enforce and be tested for cross-currency safety.
/// </summary>
public enum Currency
{
    /// <summary>Nigerian Naira.</summary>
    Ngn = 566,

    /// <summary>US Dollar. Not used by the application; present for currency-safety guards.</summary>
    Usd = 840
}
