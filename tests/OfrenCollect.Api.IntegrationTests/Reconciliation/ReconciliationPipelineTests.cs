using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OfrenCollect.Api.IntegrationTests.Persistence;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Reconciliation.HandleTransactionNotification;
using OfrenCollect.Domain.Invoices;
using OfrenCollect.Domain.Subscriptions;
using OfrenCollect.Repository.Persistence;
using OfrenCollect.Repository.Persistence.Repositories;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Api.IntegrationTests.Reconciliation;

[Collection(nameof(PostgresCollection))]
public sealed class ReconciliationPipelineTests : IAsyncLifetime
{
    private static readonly DateTimeOffset PeriodStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DueDate = new(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PaidAt = new(2026, 7, 17, 9, 13, 0, TimeSpan.Zero);

    private readonly PostgresFixture _fixture;
    private readonly IReconciliationNotifier _notifier = Substitute.For<IReconciliationNotifier>();

    public ReconciliationPipelineTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task MatchedPayment_MarksInvoicePaid_PersistsPaymentEvent_AndIsIdempotent()
    {
        var tenantId = Guid.NewGuid();
        const string accountNumber = "7080124933";
        const string reference = "MNFY-INT-1";
        var invoiceId = await SeedSubscriptionWithInvoice(tenantId, accountNumber, 5000m);

        var monnify = MonnifyReturning(reference, 5000m, accountNumber);

        await RunHandler(reference, monnify);

        await using (var db = AsTenant(tenantId))
        {
            (await db.Invoices.SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
            (await db.PaymentEvents.AnyAsync(p => p.MonnifyTransactionReference == reference)).Should().BeTrue();
        }

        // Re-deliver the same reference: no double count, invoice unchanged (FR-3.6, NFR-2.1).
        await RunHandler(reference, monnify);

        await using (var db = AsTenant(tenantId))
        {
            (await db.PaymentEvents.CountAsync(p => p.MonnifyTransactionReference == reference)).Should().Be(1);
            (await db.Invoices.SingleAsync(i => i.Id == invoiceId)).AmountPaid.Should().Be(Money.Of(5000m));
        }
    }

    [Fact]
    public async Task Underpayment_MarksInvoiceUnderpaid_WithOutstandingBalance()
    {
        var tenantId = Guid.NewGuid();
        const string accountNumber = "7080339107";
        const string reference = "MNFY-INT-UNDER";
        var invoiceId = await SeedSubscriptionWithInvoice(tenantId, accountNumber, 10000m);

        await RunHandler(reference, MonnifyReturning(reference, 7000m, accountNumber));

        await using var db = AsTenant(tenantId);
        var invoice = await db.Invoices.SingleAsync(i => i.Id == invoiceId);
        invoice.Status.Should().Be(InvoiceStatus.Underpaid);
        invoice.OutstandingBalance.Should().Be(Money.Of(3000m));
    }

    [Fact]
    public async Task PaymentIntoUnknownAccount_IsRecordedUnmatched()
    {
        const string reference = "MNFY-INT-UNMATCHED";

        await RunHandler(reference, MonnifyReturning(reference, 5000m, "9999999999"));

        await using var db = _fixture.CreateContext(new TestTenantContext(null));
        var payment = await db.PaymentEvents.SingleAsync(p => p.MonnifyTransactionReference == reference);
        payment.TenantId.Should().BeNull();
        payment.MatchedInvoiceId.Should().BeNull();
    }

    private OfrenDbContext AsTenant(Guid tenantId) => _fixture.CreateContext(new TestTenantContext(tenantId));

    private static IMonnifyClient MonnifyReturning(string reference, decimal amount, string accountNumber)
    {
        var monnify = Substitute.For<IMonnifyClient>();
        monnify.VerifyTransactionAsync(reference, Arg.Any<CancellationToken>())
            .Returns(new VerifiedTransaction(reference, Money.Of(amount), accountNumber, PaidAt, true));
        return monnify;
    }

    private async Task RunHandler(string reference, IMonnifyClient monnify)
    {
        // The webhook path has no ambient tenant.
        await using var db = _fixture.CreateContext(new TestTenantContext(null));
        var handler = new HandleTransactionNotificationHandler(
            monnify,
            new PaymentEventRepository(db),
            new SubscriptionRepository(db),
            new InvoiceRepository(db),
            new UnitOfWork(db),
            _notifier);

        await handler.Handle(new HandleTransactionNotificationCommand(reference), CancellationToken.None);
    }

    private async Task<Guid> SeedSubscriptionWithInvoice(Guid tenantId, string accountNumber, decimal amountDue)
    {
        await using var db = AsTenant(tenantId);
        var subscription = Subscription.Enrol(
            tenantId, Guid.NewGuid(), Guid.NewGuid(), $"OFREN-{accountNumber}", DueDate);
        subscription.AttachReservedAccount(accountNumber, "Wema Bank");
        db.Subscriptions.Add(subscription);

        var invoice = Invoice.Create(tenantId, subscription.Id, Money.Of(amountDue), PeriodStart, DueDate);
        db.Invoices.Add(invoice);

        await db.SaveChangesAsync();
        return invoice.Id;
    }
}
