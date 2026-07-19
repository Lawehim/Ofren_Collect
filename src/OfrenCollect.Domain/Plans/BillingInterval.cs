namespace OfrenCollect.Domain.Plans;

/// <summary>How often a plan recurs. Explicit non-zero values so an unset default is invalid.</summary>
public enum BillingInterval
{
    Weekly = 1,
    Monthly = 2,
    Yearly = 3
}
