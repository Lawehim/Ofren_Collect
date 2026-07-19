namespace OfrenCollect.SharedKernel;

/// <summary>
/// Guard clauses for enforcing invariants at construction boundaries (fail fast, CLAUDE.md §5).
/// Each returns the validated value so it can be used inline.
/// </summary>
public static class Guard
{
    /// <summary>Rejects a null, empty, or whitespace string.</summary>
    public static string AgainstNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} must not be blank.", paramName);
        }

        return value;
    }

    /// <summary>Rejects a money amount that is zero or negative.</summary>
    public static Money AgainstNonPositive(Money amount, string paramName)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (amount.Amount <= 0m)
        {
            throw new ArgumentException($"{paramName} must be greater than zero.", paramName);
        }

        return amount;
    }
}
