using FluentAssertions;
using OfrenCollect.Api.IntegrationTests.Persistence;
using OfrenCollect.Domain.Audit;
using OfrenCollect.Repository.Persistence.Repositories;

namespace OfrenCollect.Api.IntegrationTests.Audit;

[Collection(nameof(PostgresCollection))]
public sealed class AuditReaderTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Earlier = new(2026, 7, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2026, 7, 19, 9, 5, 0, TimeSpan.Zero);

    private readonly PostgresFixture _fixture;

    public AuditReaderTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetForTenant_ReturnsOnlyThatTenantsEntries_NewestFirst()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var db = _fixture.CreateContext(new TestTenantContext(null)))
        {
            db.AuditEntries.Add(Entry(tenantA, "a-early", "GET", "/api/plans", Earlier));
            db.AuditEntries.Add(Entry(tenantA, "a-late", "POST", "/api/plans", Later));
            db.AuditEntries.Add(Entry(tenantB, "b-only", "GET", "/api/dashboard", Later));
            await db.SaveChangesAsync();
        }

        await using var reader = _fixture.CreateContext(new TestTenantContext(null));
        var entries = await new AuditReader(reader).GetForTenantAsync(tenantA, 100, CancellationToken.None);

        entries.Should().HaveCount(2);
        entries[0].CorrelationId.Should().Be("a-late");
        entries[1].CorrelationId.Should().Be("a-early");
    }

    private static AuditEntry Entry(Guid tenantId, string correlationId, string method, string path, DateTimeOffset at) =>
        AuditEntry.Record(
            tenantId, userId: null, correlationId, method, path, queryString: null, requestBody: null,
            responseStatusCode: 200, responseBody: null, durationMs: 4, ipAddress: "127.0.0.1", timestampUtc: at);
}
