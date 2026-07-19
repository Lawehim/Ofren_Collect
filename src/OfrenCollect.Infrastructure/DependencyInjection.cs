using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Assistant;
using OfrenCollect.Infrastructure.Ai;
using OfrenCollect.Infrastructure.Auth;
using OfrenCollect.Infrastructure.Jobs;
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
            })
            // Retry-with-backoff, timeout, and a circuit breaker around Monnify (NFR-2.5):
            // transient faults are retried; a sustained outage trips the breaker to fail fast.
            .AddStandardResilienceHandler();

        services.AddHostedService<InboxDrainer>();

        // AI assistant (stretch, flag-gated). Off by default -> the NullAiAssistant; when enabled,
        // an OpenAI-compatible model classifies the question and the app grounds the answer.
        var aiOptions = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        services.AddSingleton(aiOptions);
        if (aiOptions.Enabled)
        {
            services.AddHttpClient<IIntentClassifier, LlmIntentClassifier>(client =>
                {
                    if (!string.IsNullOrWhiteSpace(aiOptions.BaseUrl))
                    {
                        client.BaseAddress = new Uri(aiOptions.BaseUrl);
                    }
                })
                .AddStandardResilienceHandler();
            services.AddScoped<IAiAssistant, AiAssistant>();
        }
        else
        {
            services.AddSingleton<IAiAssistant, NullAiAssistant>();
        }

        return services;
    }
}
