using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OfrenCollect.Application.Common.Behaviors;

namespace OfrenCollect.Application;

/// <summary>Registers the application layer: MediatR handlers, validators, and pipeline behaviors.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
