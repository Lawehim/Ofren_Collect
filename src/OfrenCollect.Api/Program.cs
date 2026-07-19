var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.MapControllers();

// Liveness probe — lets a judge confirm the API is up from a clean clone (M0).
// Real health checks (DB reachability, migrations applied) arrive with persistence.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Exposed so WebApplicationFactory<Program> can host the API in integration tests.
public partial class Program;
