using FluentValidation;
using MediatR;

namespace OfrenCollect.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs every registered FluentValidation validator for a
/// request before its handler, throwing <see cref="ValidationException"/> if any fail.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var validators = _validators.ToList();
        if (validators.Count > 0)
        {
            var context = new ValidationContext<TRequest>(request);
            var failures = validators
                .Select(validator => validator.Validate(context))
                .SelectMany(result => result.Errors)
                .Where(failure => failure is not null)
                .ToList();

            if (failures.Count > 0)
            {
                throw new ValidationException(failures);
            }
        }

        return await next(cancellationToken);
    }
}
