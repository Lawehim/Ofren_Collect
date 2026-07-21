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

    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    private static User NewUser() => User.Create(TenantId, "ada@brightpath.ng", "old-hash", UserRole.Owner);

    [Fact]
    public void SetPasswordResetToken_StoresHashAndExpiry()
    {
        var user = NewUser();
        var expiry = Now.AddMinutes(30);

        user.SetPasswordResetToken("token-hash", expiry);

        user.PasswordResetTokenHash.Should().Be("token-hash");
        user.PasswordResetTokenExpiresAt.Should().Be(expiry);
    }

    [Fact]
    public void IsResetTokenValid_TrueBeforeExpiry_FalseAfter_FalseWhenNone()
    {
        var user = NewUser();
        user.IsResetTokenValid(Now).Should().BeFalse();

        user.SetPasswordResetToken("h", Now.AddMinutes(30));
        user.IsResetTokenValid(Now).Should().BeTrue();
        user.IsResetTokenValid(Now.AddMinutes(31)).Should().BeFalse();
    }

    [Fact]
    public void ResetPassword_ChangesHash_AndClearsToken()
    {
        var user = NewUser();
        user.SetPasswordResetToken("h", Now.AddMinutes(30));

        user.ResetPassword("new-hash");

        user.PasswordHash.Should().Be("new-hash");
        user.PasswordResetTokenHash.Should().BeNull();
        user.PasswordResetTokenExpiresAt.Should().BeNull();
        user.IsResetTokenValid(Now).Should().BeFalse();
    }

    [Fact]
    public void SetPasswordResetToken_BlankHash_Throws()
    {
        var act = () => NewUser().SetPasswordResetToken("", Now.AddMinutes(30));

        act.Should().Throw<ArgumentException>();
    }
}
