using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Assistant;
using OfrenCollect.Infrastructure.Ai;
using OfrenCollect.Infrastructure.Auth;
using OfrenCollect.Infrastructure.Email;
using OfrenCollect.Infrastructure.Jobs;
using OfrenCollect.Infrastructure.Monnify;
using OfrenCollect.Infrastructure.Refunds;

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
        services.AddSingleton<IResetTokenService, ResetTokenService>();
        services.AddSingleton<IMonnifyWebhookVerifier, MonnifyWebhookSignatureVerifier>();

        var emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
        services.AddSingleton(emailOptions);
        // Email is non-blocking: handlers enqueue instantly; a background dispatcher delivers via
        // Brevo's HTTP API (port 443, so it works where outbound SMTP is blocked, e.g. Render).
        services.AddSingleton<IEmailOutbox, ChannelEmailOutbox>();
        services.AddSingleton<IAccountEmailService, AccountEmailService>();
        services.AddHttpClient(EmailDispatcher.HttpClientName, client =>
            {
                client.BaseAddress = new Uri(emailOptions.Provider == EmailProvider.Mailtrap
                    ? "https://sandbox.api.mailtrap.io"
                    : "https://api.brevo.com");
            })
            .AddStandardResilienceHandler();
        services.AddHostedService<EmailDispatcher>();

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

        // MonnifyClient also serves the focused refund boundary; forward to the same typed client
        // so refunds inherit its auth caching and resilience rather than duplicating them.
        services.AddTransient<IMonnifyRefundClient>(sp => (IMonnifyRefundClient)sp.GetRequiredService<IMonnifyClient>());

        var refundsOptions = configuration.GetSection(RefundsOptions.SectionName).Get<RefundsOptions>()
            ?? new RefundsOptions();
        services.AddSingleton(refundsOptions);

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
                        // Ensure a trailing slash so a relative request path appends to the base
                        // path (e.g. Groq's "/openai") instead of replacing it.
                        var baseUrl = aiOptions.BaseUrl.EndsWith('/') ? aiOptions.BaseUrl : aiOptions.BaseUrl + "/";
                        client.BaseAddress = new Uri(baseUrl);
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
