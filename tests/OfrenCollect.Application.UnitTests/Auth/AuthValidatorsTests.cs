using FluentAssertions;
using OfrenCollect.Application.Auth.Login;
using OfrenCollect.Application.Auth.Register;

namespace OfrenCollect.Application.UnitTests.Auth;

public class AuthValidatorsTests
{
    private readonly RegisterBusinessCommandValidator _register = new();
    private readonly LoginCommandValidator _login = new();

    [Fact]
    public void Register_WithValidInput_Passes()
    {
        var result = _register.Validate(new RegisterBusinessCommand("BrightPath", "ada@brightpath.ng", "password123"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "ada@brightpath.ng", "password123")]      // blank business name
    [InlineData("BrightPath", "not-an-email", "password123")] // invalid email
    [InlineData("BrightPath", "ada@brightpath.ng", "short")]  // password too short
    public void Register_WithInvalidInput_Fails(string businessName, string email, string password)
    {
        var result = _register.Validate(new RegisterBusinessCommand(businessName, email, password));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "password123")]
    [InlineData("ada@brightpath.ng", "")]
    public void Login_WithBlankFields_Fails(string email, string password)
    {
        var result = _login.Validate(new LoginCommand(email, password));

        result.IsValid.Should().BeFalse();
    }
}
