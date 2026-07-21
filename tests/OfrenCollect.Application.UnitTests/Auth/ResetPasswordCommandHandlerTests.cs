using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Auth;
using OfrenCollect.Application.Auth.ResetPassword;
using OfrenCollect.Domain.Users;

namespace OfrenCollect.Application.UnitTests.Auth;

public class ResetPasswordCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
    private const string RawToken = "raw-token";
    private const string TokenHash = "hashed-token";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IResetTokenService _tokens = Substitute.For<IResetTokenService>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public ResetPasswordCommandHandlerTests()
    {
        _tokens.Hash(RawToken).Returns(TokenHash);
        _hasher.Hash("new-password").Returns("new-hash");
    }

    private ResetPasswordCommandHandler CreateHandler() =>
        new(_users, _tokens, _hasher, _unitOfWork, new FixedClock(Now));

    private static User UserWithToken(DateTimeOffset expiry)
    {
        var user = User.Create(Guid.NewGuid(), "ada@brightpath.ng", "old-hash", UserRole.Owner);
        user.SetPasswordResetToken(TokenHash, expiry);
        return user;
    }

    [Fact]
    public async Task Handle_WithValidToken_SetsNewPasswordHash_AndSaves()
    {
        var user = UserWithToken(Now.AddMinutes(10));
        _users.FindByResetTokenHashAsync(TokenHash, Arg.Any<CancellationToken>()).Returns(user);

        await CreateHandler().Handle(new ResetPasswordCommand(RawToken, "new-password"), CancellationToken.None);

        user.PasswordHash.Should().Be("new-hash");
        user.PasswordResetTokenHash.Should().BeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTokenUnknown_Throws()
    {
        _users.FindByResetTokenHashAsync(TokenHash, Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = () => CreateHandler().Handle(new ResetPasswordCommand(RawToken, "new-password"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidResetTokenException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTokenExpired_Throws()
    {
        var user = UserWithToken(Now.AddMinutes(-1));
        _users.FindByResetTokenHashAsync(TokenHash, Arg.Any<CancellationToken>()).Returns(user);

        var act = () => CreateHandler().Handle(new ResetPasswordCommand(RawToken, "new-password"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidResetTokenException>();
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
