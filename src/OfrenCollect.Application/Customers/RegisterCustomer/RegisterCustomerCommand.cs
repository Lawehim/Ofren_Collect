using MediatR;

namespace OfrenCollect.Application.Customers.RegisterCustomer;

/// <summary>Registers a customer for the current tenant (FR-2.1).</summary>
public sealed record RegisterCustomerCommand(string Name, string Email) : IRequest<CustomerResponse>;
