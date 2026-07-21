using FluentAssertions;
using OfrenCollect.Domain.Mandates;

namespace OfrenCollect.Domain.UnitTests.Mandates;

public class MandateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubscriptionId = Guid.NewGuid();
    private static readonly DateTimeOffset RequestedAt = new(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
    private const string Reference = "OFREN-MND-1";
    private const string MonnifyCode = "MNFY-MANDATE-9";

    private static Mandate Request() => Mandate.Request(TenantId, SubscriptionId, Reference, MonnifyCode, RequestedAt);

    [Fact]
    public void Request_CreatesPendingMandate_StampedToTenantAndSubscription()
    {
        var mandate = Request();

        mandate.TenantId.Should().Be(TenantId);
        mandate.SubscriptionId.Should().Be(SubscriptionId);
        mandate.MandateReference.Should().Be(Reference);
        mandate.MonnifyMandateCode.Should().Be(MonnifyCode);
        mandate.Status.Should().Be(MandateStatus.Pending);
        mandate.IsActive.Should().BeFalse();
        mandate.ActivatedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Request_WithBlankReference_Throws(string reference)
    {
        var act = () => Mandate.Request(TenantId, SubscriptionId, reference, MonnifyCode, RequestedAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Activate_FromPending_SetsActive_WithTimestamp()
    {
        var mandate = Request();
        var activatedAt = RequestedAt.AddMinutes(5);

        mandate.Activate(activatedAt);

        mandate.Status.Should().Be(MandateStatus.Active);
        mandate.IsActive.Should().BeTrue();
        mandate.ActivatedAt.Should().Be(activatedAt);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_IsNoOp()
    {
        var mandate = Request();
        var first = RequestedAt.AddMinutes(5);
        mandate.Activate(first);

        mandate.Activate(RequestedAt.AddMinutes(9));

        mandate.ActivatedAt.Should().Be(first);
    }

    [Fact]
    public void Activate_WhenRevoked_Throws()
    {
        var mandate = Request();
        mandate.Revoke(RequestedAt.AddMinutes(3));

        var act = () => mandate.Activate(RequestedAt.AddMinutes(4));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Revoke_FromActive_SetsRevoked_AndStopsBeingActive()
    {
        var mandate = Request();
        mandate.Activate(RequestedAt.AddMinutes(5));
        var revokedAt = RequestedAt.AddMinutes(10);

        mandate.Revoke(revokedAt);

        mandate.Status.Should().Be(MandateStatus.Revoked);
        mandate.IsActive.Should().BeFalse();
        mandate.RevokedAt.Should().Be(revokedAt);
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_IsNoOp()
    {
        var mandate = Request();
        var first = RequestedAt.AddMinutes(3);
        mandate.Revoke(first);

        mandate.Revoke(RequestedAt.AddMinutes(8));

        mandate.RevokedAt.Should().Be(first);
    }
}
