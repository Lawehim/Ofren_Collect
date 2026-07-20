using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Transactions;
using OfrenCollect.Application.Transactions.ListTransactions;

namespace OfrenCollect.Application.UnitTests.Transactions;

public class ListTransactionsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTheTenantScopedRowsFromTheReader()
    {
        var reader = Substitute.For<ITransactionReader>();
        var rows = new List<TransactionRow>
        {
            new("MNFY-1", "Ada", 5000m, 1000m, 4000m, "7080000001", DateTimeOffset.UnixEpoch),
        };
        reader.ListAsync(Arg.Any<CancellationToken>()).Returns(rows);

        var result = await new ListTransactionsQueryHandler(reader).Handle(
            new ListTransactionsQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(rows);
    }
}
