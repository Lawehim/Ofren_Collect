using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Mandates.ResolveMandateStatus;
using OfrenCollect.Domain.Mandates;

namespace OfrenCollect.Application.UnitTests.Mandates;

public class ResolveMandateStatusCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
    private const string Reference = "OFREN-MND-1";

    private readonly IMandateRepository _mandates = Substitute.For<IMandateRepository>();
    private readonly IMonnifyMandateClient _monnify = Substitute.For<IMonnifyMandateClient>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private ResolveMandateStatusCommandHandler CreateHandler() =>
        new(_mandates, _monnify, _unitOfWork, new FixedClock(Now));

    private static Mandate PendingMandate() => Mandate.Request(TenantId, Guid.NewGuid(), Reference, "MTDD|ABC", Now);

    private void GivenMandate(Mandate mandate) =>
        _mandates.GetForResolutionAsync(Reference, Arg.Any<CancellationToken>()).Returns(mandate);

    [Fact]
    public async Task Handle_WhenMonnifyConfirmsActive_ActivatesPendingMandate()
    {
        var mandate = PendingMandate();
        GivenMandate(mandate);
        _monnify.GetMandateStatusAsync(Reference, Arg.Any<CancellationToken>()).Returns(MonnifyMandateStatus.Active);

        await CreateHandler().Handle(new ResolveMandateStatusCommand(Reference), CancellationToken.None);

        mandate.Status.Should().Be(MandateStatus.Active);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMonnifyReportsCancelled_RevokesActiveMandate()
    {
        var mandate = PendingMandate();
        mandate.Activate(Now);
        GivenMandate(mandate);
        _monnify.GetMandateStatusAsync(Reference, Arg.Any<CancellationToken>()).Returns(MonnifyMandateStatus.Cancelled);

        await CreateHandler().Handle(new ResolveMandateStatusCommand(Reference), CancellationToken.None);

        mandate.Status.Should().Be(MandateStatus.Revoked);
    }

    [Fact]
    public async Task Handle_WhenStillInitiated_MakesNoChange()
    {
        var mandate = PendingMandate();
        GivenMandate(mandate);
        _monnify.GetMandateStatusAsync(Reference, Arg.Any<CancellationToken>()).Returns(MonnifyMandateStatus.Initiated);

        await CreateHandler().Handle(new ResolveMandateStatusCommand(Reference), CancellationToken.None);

        mandate.Status.Should().Be(MandateStatus.Pending);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenMandateUnknown_DoesNotCallMonnify()
    {
        _mandates.GetForResolutionAsync(Reference, Arg.Any<CancellationToken>()).Returns((Mandate?)null);

        await CreateHandler().Handle(new ResolveMandateStatusCommand(Reference), CancellationToken.None);

        await _monnify.DidNotReceive().GetMandateStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
