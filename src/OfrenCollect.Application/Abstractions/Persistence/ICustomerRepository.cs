using OfrenCollect.Domain.Customers;

namespace OfrenCollect.Application.Abstractions.Persistence;

/// <summary>Reads and writes customers (tenant-scoped by the global query filter).</summary>
public interface ICustomerRepository
{
    void Add(Customer customer);

    /// <summary>Finds a customer owned by the current tenant, or null.</summary>
    Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken);
}
