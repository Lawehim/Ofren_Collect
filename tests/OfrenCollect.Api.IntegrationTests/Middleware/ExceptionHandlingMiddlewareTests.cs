using System.Text;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using OfrenCollect.Api.Middleware;
using OfrenCollect.Application.Auth;
using OfrenCollect.Application.Common;
using OfrenCollect.Infrastructure.Monnify;

namespace OfrenCollect.Api.IntegrationTests.Middleware;

// Plain unit tests — maps exceptions to safe status codes (CLAUDE.md §10). No database.
public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int Status, string Body)> Invoke(Exception thrown)
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw thrown, NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, body);
    }

    [Fact]
    public async Task ValidationException_MapsTo400_WithErrors()
    {
        var (status, body) = await Invoke(new ValidationException(new[]
        {
            new ValidationFailure("Amount", "Amount is required."),
        }));

        status.Should().Be(StatusCodes.Status400BadRequest);
        body.Should().Contain("Amount is required.");
    }

    [Fact]
    public async Task InvalidCredentials_MapsTo401()
    {
        var (status, _) = await Invoke(new InvalidCredentialsException());

        status.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task EmailAlreadyInUse_MapsTo409()
    {
        var (status, _) = await Invoke(new EmailAlreadyInUseException());

        status.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task NotFound_MapsTo404()
    {
        var (status, _) = await Invoke(new NotFoundException("Customer not found."));

        status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task MonnifyException_MapsTo502()
    {
        var (status, _) = await Invoke(new MonnifyException("provider down"));

        status.Should().Be(StatusCodes.Status502BadGateway);
    }

    [Fact]
    public async Task UnexpectedException_MapsTo500_WithoutLeakingDetails()
    {
        var (status, body) = await Invoke(new InvalidOperationException("secret internal detail"));

        status.Should().Be(StatusCodes.Status500InternalServerError);
        body.Should().NotContain("secret internal detail");
        var problem = JsonDocument.Parse(body);
        problem.RootElement.GetProperty("title").GetString().Should().Be("An unexpected error occurred.");
    }
}
