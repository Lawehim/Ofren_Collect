using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Common;
using OfrenCollect.Application.Mandates.CreateMandate;
using OfrenCollect.Domain.Customers;
using OfrenCollect.Domain.Mandates;
using OfrenCollect.Domain.Plans;
using OfrenCollect.Domain.Subscriptions;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Application.UnitTests.Mandates;

public class CreateMandateCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    private readonly ISubscriptionRepository _subscriptions = Substitute.For<ISubscriptionRepository>();
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly IPlanRepository _plans = Substitute.For<IPlanRepository>();
    private readonly IMonnifyMandateClient _monnify = Substitute.For<IMonnifyMandateClient>();
    private readonly IMandateRepository _mandates = Substitute.For<IMandateRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private readonly Subscription _subscription =
        Subscription.Enrol(TenantId, Guid.NewGuid(), Guid.NewGuid(), "OFREN-1", Now.AddMonths(1));

    public CreateMandateCommandHandlerTests()
    {
        _tenantContext.CurrentTenantId.Returns(TenantId);
        _monnify.CreateMandateAsync(Arg.Any<MandateCreationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MandateCreationResult("MTDD|ABC", string.Empty, MonnifyMandateStatus.Initiated));
        _monnify.GetMandateAuthorizationLinkAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://paylink.monnify.com/mandate-auth/real");
    }

    private CreateMandateCommandHandler CreateHandler() => new(
        _subscriptions, _customers, _plans, _monnify, _mandates, _unitOfWork, _tenantContext, new FixedClock(Now));

    private void GivenResolvableSubscription()
    {
        _subscriptions.GetByIdAsync(_subscription.Id, Arg.Any<CancellationToken>()).Returns(_subscription);
        _customers.GetByIdAsync(_subscription.CustomerId, Arg.Any<CancellationToken>())
            .Returns(Customer.Register(TenantId, "Ada", "ada@x.ng"));
        _plans.GetByIdAsync(_subscription.PlanId, Arg.Any<CancellationToken>())
            .Returns(Plan.Create(TenantId, "Premium", Money.Of(25000m), BillingInterval.Monthly));
    }

    private CreateMandateCommand Command() =>
        new(_subscription.Id, "0051762787", "044", "12 Lagos St", "08012345678");

    [Fact]
    public async Task Handle_CreatesPendingMandate_AndReturnsAuthorizationLink()
    {
        GivenResolvableSubscription();

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.AuthorizationLink.Should().Contain("mandate-auth/real");
        result.Status.Should().Be(nameof(MandateStatus.Pending));
        _mandates.Received(1).Add(Arg.Is<Mandate>(m =>
            m != null && m.TenantId == TenantId && m.SubscriptionId == _subscription.Id
            && m.MonnifyMandateCode == "MTDD|ABC" && m.Status == MandateStatus.Pending));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSubscriptionNotFound_Throws_AndDoesNotCallMonnify()
    {
        _subscriptions.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Subscription?)null);

        var act = () => CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        await _monnify.DidNotReceive().CreateMandateAsync(Arg.Any<MandateCreationRequest>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
