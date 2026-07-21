using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Auth;
using OfrenCollect.Application.Auth.Register;
using OfrenCollect.Domain.Tenants;
using OfrenCollect.Domain.Users;

namespace OfrenCollect.Application.UnitTests.Auth;

public class RegisterBusinessCommandHandlerTests
{
    private const string NormalisedEmail = "ada@brightpath.ng";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ITenantRepository _tenants = Substitute.For<ITenantRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwt = Substitute.For<IJwtTokenService>();
    private readonly IAccountEmailService _emails = Substitute.For<IAccountEmailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private RegisterBusinessCommandHandler CreateHandler() =>
        new(_users, _tenants, _hasher, _jwt, _emails, _unitOfWork, TimeProvider.System);

    private static RegisterBusinessCommand Command() =>
        new("BrightPath Tutors", "Ada@BrightPath.NG", "password123");

    [Fact]
    public async Task Register_CreatesTenantAndOwner_HashesPassword_ReturnsToken()
    {
        _users.ExistsByEmailAsync(NormalisedEmail, Arg.Any<CancellationToken>()).Returns(false);
        _hasher.Hash("password123").Returns("hashed");
        _jwt.GenerateToken(Arg.Any<Guid>(), Arg.Any<Guid>(), NormalisedEmail, UserRole.Owner).Returns("token");

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.Token.Should().Be("token");
        result.Role.Should().Be(nameof(UserRole.Owner));
        _tenants.Received(1).Add(Arg.Any<Tenant>());
        _users.Received(1).Add(Arg.Is<User>(u =>
            u != null && u.Email == NormalisedEmail && u.PasswordHash == "hashed" && u.Role == UserRole.Owner));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_WhenEmailAlreadyInUse_Throws_AndDoesNotSave()
    {
        _users.ExistsByEmailAsync(NormalisedEmail, Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<EmailAlreadyInUseException>();
        _users.DidNotReceive().Add(Arg.Any<User>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
