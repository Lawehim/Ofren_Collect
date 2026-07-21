using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Common;
using OfrenCollect.Application.Mandates.CancelMandate;
using OfrenCollect.Domain.Mandates;

namespace OfrenCollect.Application.UnitTests.Mandates;

public class CancelMandateCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
    private const string Reference = "OFREN-MND-1";

    private readonly IMandateRepository _mandates = Substitute.For<IMandateRepository>();
    private readonly IMonnifyMandateClient _monnify = Substitute.For<IMonnifyMandateClient>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    public CancelMandateCommandHandlerTests() => _tenantContext.CurrentTenantId.Returns(TenantId);

    private CancelMandateCommandHandler CreateHandler() =>
        new(_mandates, _monnify, _unitOfWork, _tenantContext, new FixedClock(Now));

    private static Mandate ActiveMandate()
    {
        var mandate = Mandate.Request(TenantId, Guid.NewGuid(), Reference, "MTDD|ABC", Now);
        mandate.Activate(Now);
        return mandate;
    }

    [Fact]
    public async Task Handle_WhenActive_CancelsWithMonnify_AndRevokes()
    {
        var mandate = ActiveMandate();
        _mandates.GetByReferenceAsync(Reference, Arg.Any<CancellationToken>()).Returns(mandate);

        await CreateHandler().Handle(new CancelMandateCommand(Reference), CancellationToken.None);

        mandate.Status.Should().Be(MandateStatus.Revoked);
        await _monnify.Received(1).CancelMandateAsync("MTDD|ABC", Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAlreadyRevoked_IsNoOp()
    {
        var mandate = ActiveMandate();
        mandate.Revoke(Now);
        _mandates.GetByReferenceAsync(Reference, Arg.Any<CancellationToken>()).Returns(mandate);

        await CreateHandler().Handle(new CancelMandateCommand(Reference), CancellationToken.None);

        await _monnify.DidNotReceive().CancelMandateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_Throws()
    {
        _mandates.GetByReferenceAsync(Reference, Arg.Any<CancellationToken>()).Returns((Mandate?)null);

        var act = () => CreateHandler().Handle(new CancelMandateCommand(Reference), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
