using MediatR;

namespace OfrenCollect.Application.Subscriptions.EnrolCustomer;

/// <summary>
/// Enrols a customer on a plan: creates a subscription, provisions a Monnify reserved account,
/// and creates the first invoice (FR-2.2, FR-2.3, FR-2.5).
/// </summary>
public sealed record EnrolCustomerCommand(Guid CustomerId, Guid PlanId) : IRequest<SubscriptionResponse>;
