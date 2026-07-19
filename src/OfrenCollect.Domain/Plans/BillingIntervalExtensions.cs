namespace OfrenCollect.Domain.Plans;

/// <summary>Date arithmetic for billing intervals — used to derive invoice due dates (FR-2.5).</summary>
public static class BillingIntervalExtensions
{
    /// <summary>Returns the date one interval after <paramref name="from"/>.</summary>
    public static DateTimeOffset NextDueDateFrom(this BillingInterval interval, DateTimeOffset from) =>
        interval switch
        {
            BillingInterval.Weekly => from.AddDays(7),
            BillingInterval.Monthly => from.AddMonths(1),
            BillingInterval.Yearly => from.AddYears(1),
            _ => throw new ArgumentOutOfRangeException(
                nameof(interval), interval, "Unknown billing interval.")
        };
}
