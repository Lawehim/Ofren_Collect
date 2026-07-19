using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Infrastructure.Auth;
using OfrenCollect.Infrastructure.Monnify;

namespace OfrenCollect.Infrastructure;

/// <summary>Registers external integrations: Monnify, JWT, password hashing, and the clock.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        var monnifyOptions = configuration.GetSection(MonnifyOptions.SectionName).Get<MonnifyOptions>()
            ?? new MonnifyOptions();
        services.AddSingleton(monnifyOptions);

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? new JwtOptions();
        services.AddSingleton(jwtOptions);

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IMonnifyWebhookVerifier, MonnifyWebhookSignatureVerifier>();

        services.AddHttpClient<IMonnifyClient, MonnifyClient>(client =>
        {
            if (!string.IsNullOrWhiteSpace(monnifyOptions.BaseUrl))
            {
                client.BaseAddress = new Uri(monnifyOptions.BaseUrl);
            }
        });

        return services;
    }
}
