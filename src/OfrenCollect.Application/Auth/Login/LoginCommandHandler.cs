using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;

namespace OfrenCollect.Application.Auth.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResult>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwt;

    public LoginCommandHandler(IUserRepository users, IPasswordHasher passwordHasher, IJwtTokenService jwt)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
    }

    public async Task<AuthResult> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var user = await _users.FindByEmailAsync(email, cancellationToken);

        // Same failure for unknown email and wrong password — never reveal which (TC-0.4).
        if (user is null || !_passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        var token = _jwt.GenerateToken(user.Id, user.TenantId, user.Email, user.Role);
        return new AuthResult(token, user.Email, user.Role.ToString());
    }
}
