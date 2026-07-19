using FluentAssertions;
using OfrenCollect.Api.IntegrationTests.Persistence;
using OfrenCollect.Domain.Webhooks;
using OfrenCollect.Repository.Persistence.Repositories;

namespace OfrenCollect.Api.IntegrationTests.Webhooks;

[Collection(nameof(PostgresCollection))]
public sealed class InboxRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset First = new(2026, 7, 19, 21, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Second = new(2026, 7, 19, 21, 1, 0, TimeSpan.Zero);

    private readonly PostgresFixture _fixture;

    public InboxRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Unprocessed_ReturnedOldestFirst_ThenExcludedOnceProcessed()
    {
        await using (var db = _fixture.CreateContext(new TestTenantContext(null)))
        {
            var repository = new InboxRepository(db);
            repository.Add(InboxMessage.Receive("MNFY|1", "4003115967", "{}", First));
            repository.Add(InboxMessage.Receive("MNFY|2", "4003228858", "{}", Second));
            await db.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateContext(new TestTenantContext(null)))
        {
            var repository = new InboxRepository(db);
            var unprocessed = await repository.GetUnprocessedAsync(10, CancellationToken.None);

            unprocessed.Should().HaveCount(2);
            unprocessed[0].TransactionReference.Should().Be("MNFY|1");

            unprocessed[0].MarkProcessed(Second.AddSeconds(3));
            await db.SaveChangesAsync();
        }

        await using (var verify = _fixture.CreateContext(new TestTenantContext(null)))
        {
            var remaining = await new InboxRepository(verify).GetUnprocessedAsync(10, CancellationToken.None);

            remaining.Should().ContainSingle();
            remaining[0].TransactionReference.Should().Be("MNFY|2");
        }
    }
}
