using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Common;
using OfrenCollect.Application.Subscriptions.EnrolCustomer;
using OfrenCollect.Domain.Customers;
using OfrenCollect.Domain.Invoices;
using OfrenCollect.Domain.Plans;
using OfrenCollect.Domain.Subscriptions;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Application.UnitTests.Subscriptions;

public class EnrolCustomerCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly IPlanRepository _plans = Substitute.For<IPlanRepository>();
    private readonly ISubscriptionRepository _subscriptions = Substitute.For<ISubscriptionRepository>();
    private readonly IInvoiceRepository _invoices = Substitute.For<IInvoiceRepository>();
    private readonly IMonnifyClient _monnify = Substitute.For<IMonnifyClient>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private readonly Customer _customer = Customer.Register(TenantId, "Chidi Eze", "chidi@mail.com");
    private readonly Plan _plan = Plan.Create(TenantId, "Premium", Money.Of(25000m), BillingInterval.Monthly);

    public EnrolCustomerCommandHandlerTests()
    {
        _tenantContext.CurrentTenantId.Returns(TenantId);
        _customers.GetByIdAsync(_customer.Id, Arg.Any<CancellationToken>()).Returns(_customer);
        _plans.GetByIdAsync(_plan.Id, Arg.Any<CancellationToken>()).Returns(_plan);
    }

    private EnrolCustomerCommandHandler CreateHandler() =>
        new(_customers, _plans, _subscriptions, _invoices, _monnify, _unitOfWork, _tenantContext, TimeProvider.System);

    private EnrolCustomerCommand Command() => new(_customer.Id, _plan.Id);

    [Fact]
    public async Task Handle_CreatesSubscriptionWithReservedAccountAndFirstInvoice()
    {
        _monnify.CreateReservedAccountAsync(Arg.Any<CreateReservedAccountRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReservedAccount("7080124933", "Wema Bank"));

        var response = await CreateHandler().Handle(Command(), CancellationToken.None);

        response.ReservedAccountNumber.Should().Be("7080124933");
        response.Status.Should().Be(nameof(SubscriptionStatus.Active));
        _subscriptions.Received(1).Add(Arg.Is<Subscription>(s =>
            s != null && s.CustomerId == _customer.Id && s.ReservedAccountNumber == "7080124933"));
        _invoices.Received(1).Add(Arg.Is<Invoice>(i => i != null && i.AmountDue == Money.Of(25000m)));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCustomerNotFound_Throws_WithoutCallingMonnifyOrSaving()
    {
        _customers.GetByIdAsync(_customer.Id, Arg.Any<CancellationToken>()).Returns((Customer?)null);

        var act = async () => await CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        await _monnify.DidNotReceive().CreateReservedAccountAsync(
            Arg.Any<CreateReservedAccountRequest>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMonnifyFails_PersistsNothing()
    {
        _monnify.CreateReservedAccountAsync(Arg.Any<CreateReservedAccountRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Monnify down"));

        var act = async () => await CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _subscriptions.DidNotReceive().Add(Arg.Any<Subscription>());
        _invoices.DidNotReceive().Add(Arg.Any<Invoice>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
