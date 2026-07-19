namespace OfrenCollect.Api.IntegrationTests.Persistence;

/// <summary>Shares one PostgreSQL container across the persistence integration tests.</summary>
[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
