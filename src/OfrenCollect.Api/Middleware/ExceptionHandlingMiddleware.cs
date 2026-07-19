using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OfrenCollect.Application.Auth;
using OfrenCollect.Application.Common;
using OfrenCollect.Infrastructure.Monnify;

namespace OfrenCollect.Api.Middleware;

/// <summary>
/// Single place that turns exceptions into uniform problem responses. Never leaks stack traces
/// or internals to clients (CLAUDE.md §10). Unexpected errors are logged and returned as 500.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "One or more validation errors occurred."),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, exception.Message),
            EmailAlreadyInUseException => (StatusCodes.Status409Conflict, exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            MonnifyException => (StatusCodes.Status502BadGateway, "The payment provider is unavailable."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception processing {Path}", context.Request.Path);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title
        };

        if (exception is ValidationException validationException)
        {
            problem.Extensions["errors"] = validationException.Errors
                .Select(error => error.ErrorMessage)
                .ToArray();
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
