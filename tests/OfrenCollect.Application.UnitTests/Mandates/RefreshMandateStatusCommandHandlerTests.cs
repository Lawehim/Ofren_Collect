using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Common;
using OfrenCollect.Application.Mandates.RefreshMandateStatus;
using OfrenCollect.Domain.Mandates;

namespace OfrenCollect.Application.UnitTests.Mandates;

public class RefreshMandateStatusCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
    private const string Reference = "OFREN-MND-1";

    private readonly IMandateRepository _mandates = Substitute.For<IMandateRepository>();
    private readonly IMonnifyMandateClient _monnify = Substitute.For<IMonnifyMandateClient>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    public RefreshMandateStatusCommandHandlerTests() => _tenantContext.CurrentTenantId.Returns(TenantId);

    private RefreshMandateStatusCommandHandler CreateHandler() =>
        new(_mandates, _monnify, _unitOfWork, _tenantContext, new FixedClock(Now));

    private static Mandate PendingMandate() =>
        Mandate.Request(TenantId, Guid.NewGuid(), Reference, "MTDD|ABC", Now);

    private void GivenMandate(Mandate mandate) =>
        _mandates.GetByReferenceAsync(Reference, Arg.Any<CancellationToken>()).Returns(mandate);

    [Fact]
    public async Task Handle_WhenMonnifyReportsActive_ActivatesTheMandate()
    {
        var mandate = PendingMandate();
        GivenMandate(mandate);
        _monnify.GetMandateStatusAsync(Reference, Arg.Any<CancellationToken>()).Returns(MonnifyMandateStatus.Active);

        var result = await CreateHandler().Handle(new RefreshMandateStatusCommand(Reference), CancellationToken.None);

        result.Status.Should().Be(nameof(MandateStatus.Active));
        mandate.Status.Should().Be(MandateStatus.Active);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenStillInitiated_LeavesPending_WithoutSaving()
    {
        var mandate = PendingMandate();
        GivenMandate(mandate);
        _monnify.GetMandateStatusAsync(Reference, Arg.Any<CancellationToken>()).Returns(MonnifyMandateStatus.Initiated);

        var result = await CreateHandler().Handle(new RefreshMandateStatusCommand(Reference), CancellationToken.None);

        result.Status.Should().Be(nameof(MandateStatus.Pending));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAlreadyActive_DoesNotReCheckMonnify()
    {
        var mandate = PendingMandate();
        mandate.Activate(Now);
        GivenMandate(mandate);

        await CreateHandler().Handle(new RefreshMandateStatusCommand(Reference), CancellationToken.None);

        await _monnify.DidNotReceive().GetMandateStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMandateNotFound_Throws()
    {
        _mandates.GetByReferenceAsync(Reference, Arg.Any<CancellationToken>()).Returns((Mandate?)null);

        var act = () => CreateHandler().Handle(new RefreshMandateStatusCommand(Reference), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
