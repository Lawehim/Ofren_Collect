using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Tenants;
using OfrenCollect.Domain.Users;

namespace OfrenCollect.Application.Auth.Register;

public sealed class RegisterBusinessCommandHandler : IRequestHandler<RegisterBusinessCommand, AuthResult>
{
    private readonly IUserRepository _users;
    private readonly ITenantRepository _tenants;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwt;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public RegisterBusinessCommandHandler(
        IUserRepository users,
        ITenantRepository tenants,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwt,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _users = users;
        _tenants = tenants;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<AuthResult> Handle(RegisterBusinessCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();

        if (await _users.ExistsByEmailAsync(email, cancellationToken))
        {
            throw new EmailAlreadyInUseException();
        }

        var tenant = Tenant.Register(command.BusinessName, _clock.GetUtcNow());
        var user = User.Create(tenant.Id, email, _passwordHasher.Hash(command.Password), UserRole.Owner);

        _tenants.Add(tenant);
        _users.Add(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var token = _jwt.GenerateToken(user.Id, user.TenantId, user.Email, user.Role);
        return new AuthResult(token, user.Email, user.Role.ToString());
    }
}
