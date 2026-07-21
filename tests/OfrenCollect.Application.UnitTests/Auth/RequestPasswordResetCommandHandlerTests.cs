using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Auth.ForgotPassword;
using OfrenCollect.Domain.Users;

namespace OfrenCollect.Application.UnitTests.Auth;

public class RequestPasswordResetCommandHandlerTests
{
    private const string Email = "ada@brightpath.ng";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IResetTokenService _tokens = Substitute.For<IResetTokenService>();
    private readonly IAccountEmailService _emails = Substitute.For<IAccountEmailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private RequestPasswordResetCommandHandler CreateHandler() =>
        new(_users, _tokens, _emails, _unitOfWork, TimeProvider.System);

    [Fact]
    public async Task Handle_WhenUserExists_SetsToken_Saves_AndEmailsRawToken()
    {
        var user = User.Create(Guid.NewGuid(), Email, "hash", UserRole.Owner);
        _users.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);
        _tokens.Create().Returns(new ResetToken("raw-token", "hashed-token"));

        await CreateHandler().Handle(new RequestPasswordResetCommand("Ada@BrightPath.NG"), CancellationToken.None);

        user.PasswordResetTokenHash.Should().Be("hashed-token");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _emails.Received(1).SendPasswordResetAsync(Email, "raw-token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_DoesNothing_AndSendsNoEmail()
    {
        _users.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        await CreateHandler().Handle(new RequestPasswordResetCommand("nobody@x.ng"), CancellationToken.None);

        _tokens.DidNotReceive().Create();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _emails.DidNotReceive().SendPasswordResetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
