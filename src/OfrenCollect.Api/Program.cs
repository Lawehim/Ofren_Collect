using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OfrenCollect.Api.Auth;
using OfrenCollect.Api.Hubs;
using OfrenCollect.Api.Middleware;
using OfrenCollect.Api.Persistence;
using OfrenCollect.Api.Realtime;
using OfrenCollect.Application;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Infrastructure;
using OfrenCollect.Infrastructure.Auth;
using OfrenCollect.Repository;
using OfrenCollect.Repository.Persistence;
using Scalar.AspNetCore;
using Serilog;

const string SpaCorsPolicy = "spa";
const string AuthRateLimitPolicy = "auth";

var builder = WebApplication.CreateBuilder(args);

// Hosts like Render/Koyeb inject the port to listen on via PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpContextTenantContext>();
builder.Services.AddScoped<IReconciliationNotifier, SignalRReconciliationNotifier>();
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddRepository(
    builder.Configuration.GetConnectionString("OfrenDb")
    ?? throw new InvalidOperationException("Connection string 'OfrenDb' is not configured."));
builder.Services.AddInfrastructure(builder.Configuration);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claims under their original JSON names (sub, role, tenant_id) so role checks and
        // tenant resolution read them directly, without the legacy inbound-claim remapping.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            RoleClaimType = JwtTokenService.RoleClaim
        };

        // SignalR passes the JWT via the access_token query string on the WebSocket handshake.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddCors(options => options.AddPolicy(SpaCorsPolicy, policy => policy
    .WithOrigins(builder.Configuration["Cors:Origin"] ?? "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var perTenantLimit = builder.Configuration.GetValue("RateLimiting:PerTenantPermitLimit", 100);
var perIpLimit = builder.Configuration.GetValue("RateLimiting:PerIpPermitLimit", 10);
var rateLimitWindow = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global: per tenant on authenticated routes, falling back to per IP when anonymous.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key = context.User.FindFirstValue(JwtTokenService.TenantIdClaim)
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(
            key, _ => new FixedWindowRateLimiterOptions { PermitLimit = perTenantLimit, Window = rateLimitWindow });
    });

    // Tighter per-IP limit for anonymous auth and the webhook (blunts brute force / floods).
    options.AddPolicy(AuthRateLimitPolicy, context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"auth:{ip}", _ => new FixedWindowRateLimiterOptions { PermitLimit = perIpLimit, Window = rateLimitWindow });
    });

    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter =
            ((int)rateLimitWindow.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        return ValueTask.CompletedTask;
    };
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<OfrenDbContext>();
    await database.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(CancellationToken.None);
}

// Behind the host's TLS-terminating proxy: trust the forwarded scheme/IP.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});

app.UseMiddleware<AuditLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// The edge proxy already serves HTTPS in production; redirecting there causes loops.
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}
app.UseCors(SpaCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// Public API documentation: the OpenAPI spec at /openapi/v1.json and an interactive Scalar UI at
// /docs. Anonymous so reviewers can browse the endpoints without a token.
app.MapOpenApi().AllowAnonymous();
app.MapScalarApiReference("/docs", options => options
    .WithTitle("Ofren Collect API")
    .WithTheme(ScalarTheme.BluePlanet))
    .AllowAnonymous();

app.Run();

// Exposed so WebApplicationFactory<Program> can host the API in integration tests.
public partial class Program;
