using Microsoft.EntityFrameworkCore;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Users;

namespace OfrenCollect.Repository.Persistence.Repositories;

/// <inheritdoc />
public sealed class UserRepository : IUserRepository
{
    private readonly OfrenDbContext _db;

    public UserRepository(OfrenDbContext db) => _db = db;

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken) =>
        // Pre-auth (registration): no ambient tenant, and email is globally unique.
        _db.Users.IgnoreQueryFilters().AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken);

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public void Add(User user) => _db.Users.Add(user);
}
