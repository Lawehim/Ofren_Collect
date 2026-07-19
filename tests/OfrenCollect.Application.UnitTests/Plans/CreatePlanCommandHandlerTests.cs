using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Plans.CreatePlan;
using OfrenCollect.Domain.Plans;

namespace OfrenCollect.Application.UnitTests.Plans;

public class CreatePlanCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly IPlanRepository _plans = Substitute.For<IPlanRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private CreatePlanCommandHandler CreateHandler() => new(_plans, _unitOfWork, _tenantContext);

    [Fact]
    public async Task Handle_AddsPlanForCurrentTenant_AndReturnsResponse()
    {
        _tenantContext.CurrentTenantId.Returns(TenantId);

        var response = await CreateHandler().Handle(
            new CreatePlanCommand("Basic", 5000m, BillingInterval.Monthly), CancellationToken.None);

        response.Name.Should().Be("Basic");
        response.Amount.Should().Be(5000m);
        response.Interval.Should().Be(nameof(BillingInterval.Monthly));
        response.IsActive.Should().BeTrue();
        _plans.Received(1).Add(Arg.Is<Plan>(p => p != null && p.TenantId == TenantId && p.Name == "Basic"));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_RejectsZeroAmountAndBlankName()
    {
        var validator = new CreatePlanCommandValidator();

        validator.Validate(new CreatePlanCommand("", 0m, BillingInterval.Monthly)).IsValid.Should().BeFalse();
        validator.Validate(new CreatePlanCommand("Basic", 5000m, BillingInterval.Monthly)).IsValid.Should().BeTrue();
    }
}
