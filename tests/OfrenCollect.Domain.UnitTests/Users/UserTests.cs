using FluentAssertions;
using OfrenCollect.Domain.Users;

namespace OfrenCollect.Domain.UnitTests.Users;

public class UserTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_SetsFields()
    {
        var user = User.Create(TenantId, "ada@brightpath.ng", "hashed-secret", UserRole.Owner);

        user.TenantId.Should().Be(TenantId);
        user.Email.Should().Be("ada@brightpath.ng");
        user.PasswordHash.Should().Be("hashed-secret");
        user.Role.Should().Be(UserRole.Owner);
    }

    [Fact]
    public void Create_NormalizesEmailToLowerCase()
    {
        var user = User.Create(TenantId, "Ada@BrightPath.NG", "hashed-secret", UserRole.Staff);

        user.Email.Should().Be("ada@brightpath.ng");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankEmail_Throws(string email)
    {
        var act = () => User.Create(TenantId, email, "hashed-secret", UserRole.Owner);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankPasswordHash_Throws(string passwordHash)
    {
        var act = () => User.Create(TenantId, "ada@brightpath.ng", passwordHash, UserRole.Owner);

        act.Should().Throw<ArgumentException>();
    }
}
