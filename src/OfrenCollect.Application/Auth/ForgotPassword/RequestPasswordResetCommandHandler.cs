using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;

namespace OfrenCollect.Application.Auth.ForgotPassword;

public sealed class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand>
{
    // Reset links are short-lived to limit the window if an email is intercepted.
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    private readonly IUserRepository _users;
    private readonly IResetTokenService _tokens;
    private readonly IAccountEmailService _emails;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public RequestPasswordResetCommandHandler(
        IUserRepository users,
        IResetTokenService tokens,
        IAccountEmailService emails,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _users = users;
        _tokens = tokens;
        _emails = emails;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var user = await _users.FindByEmailAsync(email, cancellationToken);

        // Anti-enumeration: return success whether or not the account exists — never disclose it.
        if (user is null)
        {
            return;
        }

        var token = _tokens.Create();
        user.SetPasswordResetToken(token.HashedToken, _clock.GetUtcNow().Add(TokenLifetime));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emails.SendPasswordResetAsync(user.Email, token.RawToken, cancellationToken);
    }
}
