using FluentAssertions;
using FluentValidation;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Mandates.DebitMandate;
using OfrenCollect.Domain.Customers;
using OfrenCollect.Domain.Invoices;
using OfrenCollect.Domain.Mandates;
using OfrenCollect.Domain.Subscriptions;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Application.UnitTests.Mandates;

public class DebitMandateCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    private readonly ISubscriptionRepository _subscriptions = Substitute.For<ISubscriptionRepository>();
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly IMandateRepository _mandates = Substitute.For<IMandateRepository>();
    private readonly IInvoiceRepository _invoices = Substitute.For<IInvoiceRepository>();
    private readonly IMandateDebitRepository _debits = Substitute.For<IMandateDebitRepository>();
    private readonly IMonnifyMandateClient _monnify = Substitute.For<IMonnifyMandateClient>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private readonly Subscription _subscription =
        Subscription.Enrol(TenantId, Guid.NewGuid(), Guid.NewGuid(), "OFREN-1", Now.AddMonths(1));
    private readonly Invoice _invoice;

    public DebitMandateCommandHandlerTests()
    {
        _tenantContext.CurrentTenantId.Returns(TenantId);
        _invoice = Invoice.Create(TenantId, _subscription.Id, Money.Of(5000m), Now, Now.AddDays(-1));

        _subscriptions.GetByIdAsync(_subscription.Id, Arg.Any<CancellationToken>()).Returns(_subscription);
        var mandate = Mandate.Request(TenantId, _subscription.Id, "OFREN-MND-1", "MTDD|ABC", Now);
        mandate.Activate(Now);
        _mandates.GetActiveBySubscriptionAsync(_subscription.Id, Arg.Any<CancellationToken>()).Returns(mandate);
        _invoices.GetOpenInvoiceForSubscriptionAsync(_subscription.Id, Arg.Any<CancellationToken>()).Returns(_invoice);
        _customers.GetByIdAsync(_subscription.CustomerId, Arg.Any<CancellationToken>())
            .Returns(Customer.Register(TenantId, "Ada", "ada@x.ng"));
        _monnify.DebitMandateAsync(Arg.Any<MandateDebitRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MandateDebitResult("MNFY|TX-1", "PENDING"));
    }

    private DebitMandateCommandHandler CreateHandler() => new(
        _subscriptions, _customers, _mandates, _invoices, _debits, _monnify, _unitOfWork, _tenantContext, new FixedClock(Now));

    [Fact]
    public async Task Handle_DebitsForOutstandingAmount_AndStoresPendingDebit()
    {
        _debits.HasActiveDebitForInvoiceAsync(_invoice.Id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(new DebitMandateCommand(_subscription.Id), CancellationToken.None);

        result.Status.Should().Be("PENDING");
        _debits.Received(1).Add(Arg.Is<MandateDebit>(d =>
            d != null && d.InvoiceId == _invoice.Id && d.Amount == Money.Of(5000m)));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenInvoiceAlreadyHasActiveDebit_Throws_AndDoesNotDebit()
    {
        _debits.HasActiveDebitForInvoiceAsync(_invoice.Id, Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateHandler().Handle(new DebitMandateCommand(_subscription.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await _monnify.DidNotReceive().DebitMandateAsync(Arg.Any<MandateDebitRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoActiveMandate_Throws()
    {
        _mandates.GetActiveBySubscriptionAsync(_subscription.Id, Arg.Any<CancellationToken>()).Returns((Mandate?)null);

        var act = () => CreateHandler().Handle(new DebitMandateCommand(_subscription.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
