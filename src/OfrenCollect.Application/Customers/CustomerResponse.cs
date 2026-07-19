using OfrenCollect.Domain.Customers;

namespace OfrenCollect.Application.Customers;

/// <summary>A customer as returned to the client.</summary>
public sealed record CustomerResponse(Guid Id, string Name, string Email)
{
    public static CustomerResponse From(Customer customer) =>
        new(customer.Id, customer.Name, customer.Email);
}
