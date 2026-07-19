using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Customers;

namespace OfrenCollect.Repository.Persistence.Repositories;

/// <inheritdoc />
public sealed class CustomerRepository : ICustomerRepository
{
    private readonly OfrenDbContext _db;

    public CustomerRepository(OfrenDbContext db) => _db = db;

    public void Add(Customer customer) => _db.Customers.Add(customer);

    public Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
}
