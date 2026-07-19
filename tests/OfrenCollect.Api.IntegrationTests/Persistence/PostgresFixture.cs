using Microsoft.EntityFrameworkCore;
using Npgsql;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Repository.Persistence;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace OfrenCollect.Api.IntegrationTests.Persistence;

/// <summary>
/// Spins up a real PostgreSQL container (CLAUDE.md §4.3 — test what you ship), applies the
/// EF migrations once, and resets data between tests with Respawn so each test is independent.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17").Build();

    private Respawner _respawner = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using (var db = CreateContext(new TestTenantContext(null)))
        {
            await db.Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Table("__EFMigrationsHistory")]
        });
    }

    /// <summary>Creates a context bound to the container, acting as the given tenant.</summary>
    public OfrenDbContext CreateContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<OfrenDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new OfrenDbContext(options, tenantContext);
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
