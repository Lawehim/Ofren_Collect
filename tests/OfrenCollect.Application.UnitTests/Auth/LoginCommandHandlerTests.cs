using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Auth;
using OfrenCollect.Application.Auth.Login;
using OfrenCollect.Domain.Users;

namespace OfrenCollect.Application.UnitTests.Auth;

public class LoginCommandHandlerTests
{
    private const string Email = "ada@brightpath.ng";
    private const string Password = "password123";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwt = Substitute.For<IJwtTokenService>();

    private LoginCommandHandler CreateHandler() => new(_users, _hasher, _jwt);

    private static User ExistingUser() =>
        User.Create(Guid.NewGuid(), Email, "stored-hash", UserRole.Owner);

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        var user = ExistingUser();
        _users.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Password, "stored-hash").Returns(true);
        _jwt.GenerateToken(user.Id, user.TenantId, Email, UserRole.Owner).Returns("token");

        var result = await CreateHandler().Handle(new LoginCommand(Email, Password), CancellationToken.None);

        result.Token.Should().Be("token");
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ThrowsInvalidCredentials()
    {
        _users.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = async () => await CreateHandler().Handle(new LoginCommand(Email, Password), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ThrowsInvalidCredentials()
    {
        var user = ExistingUser();
        _users.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Password, "stored-hash").Returns(false);

        var act = async () => await CreateHandler().Handle(new LoginCommand(Email, Password), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }
}
