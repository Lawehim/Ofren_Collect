using OfrenCollect.Domain.Subscriptions;

namespace OfrenCollect.Application.Subscriptions;

/// <summary>A subscription as returned to the client, including its reserved account to share.</summary>
public sealed record SubscriptionResponse(
    Guid Id,
    Guid CustomerId,
    Guid PlanId,
    string? ReservedAccountNumber,
    string? ReservedBankName,
    DateTimeOffset NextDueDate,
    string Status)
{
    public static SubscriptionResponse From(Subscription subscription) =>
        new(
            subscription.Id,
            subscription.CustomerId,
            subscription.PlanId,
            subscription.ReservedAccountNumber,
            subscription.ReservedBankName,
            subscription.NextDueDate,
            subscription.Status.ToString());
}
