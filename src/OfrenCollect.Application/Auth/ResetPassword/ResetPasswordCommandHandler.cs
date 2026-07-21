using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;

namespace OfrenCollect.Application.Auth.ResetPassword;

public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IUserRepository _users;
    private readonly IResetTokenService _tokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public ResetPasswordCommandHandler(
        IUserRepository users,
        IResetTokenService tokens,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _users = users;
        _tokens = tokens;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var tokenHash = _tokens.Hash(command.Token);
        var user = await _users.FindByResetTokenHashAsync(tokenHash, cancellationToken);

        if (user is null || !user.IsResetTokenValid(_clock.GetUtcNow()))
        {
            throw new InvalidResetTokenException();
        }

        // Sets the new hash and clears the token, so the link is single-use.
        user.ResetPassword(_passwordHasher.Hash(command.NewPassword));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
