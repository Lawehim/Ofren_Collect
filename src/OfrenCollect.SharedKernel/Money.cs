using System.Globalization;

namespace OfrenCollect.SharedKernel;

/// <summary>
/// A non-negative monetary amount together with its currency. Money is a value object:
/// two instances with the same amount and currency are equal. Amounts are held as
/// <see cref="decimal"/> (never floating point, per CLAUDE.md §11) and limited to two
/// decimal places (kobo). Arithmetic and comparison across different currencies is rejected.
/// </summary>
public sealed record Money : IComparable<Money>
{
    private const int MoneyDecimalPlaces = 2;

    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>The amount, non-negative and at most two decimal places.</summary>
    public decimal Amount { get; }

    /// <summary>The currency the amount is denominated in.</summary>
    public Currency Currency { get; }

    /// <summary>Creates a money value, validating that it is non-negative and within two decimal places.</summary>
    public static Money Of(decimal amount, Currency currency = Currency.Ngn)
    {
        if (amount < 0m)
        {
            throw new ArgumentException("Money amount cannot be negative.", nameof(amount));
        }

        if (amount != decimal.Round(amount, MoneyDecimalPlaces))
        {
            throw new ArgumentException(
                $"Money amount cannot have more than {MoneyDecimalPlaces} decimal places.",
                nameof(amount));
        }

        return new Money(amount, currency);
    }

    /// <summary>Zero money in the given currency.</summary>
    public static Money Zero(Currency currency = Currency.Ngn) => new(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        EnsureSameCurrency(left, right);

        var result = left.Amount - right.Amount;
        if (result < 0m)
        {
            throw new InvalidOperationException("Money cannot be negative.");
        }

        return new Money(result, left.Currency);
    }

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    /// <summary>Named alternate for the addition operator.</summary>
    public Money Add(Money other) => this + other;

    /// <summary>Named alternate for the subtraction operator.</summary>
    public Money Subtract(Money other) => this - other;

    /// <inheritdoc />
    public int CompareTo(Money? other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameCurrency(this, other);
        return decimal.Compare(Amount, other.Amount);
    }

    public override string ToString() =>
        $"{Currency.ToString().ToUpperInvariant()} {Amount.ToString("F2", CultureInfo.InvariantCulture)}";

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot operate on Money of different currencies ({left.Currency} and {right.Currency}).");
        }
    }
}
