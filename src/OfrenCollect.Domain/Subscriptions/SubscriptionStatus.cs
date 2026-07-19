namespace OfrenCollect.Domain.Subscriptions;

/// <summary>Lifecycle state of a subscription. Non-zero values so an unset default is invalid.</summary>
public enum SubscriptionStatus
{
    Active = 1,
    Overdue = 2,
    Cancelled = 3
}
