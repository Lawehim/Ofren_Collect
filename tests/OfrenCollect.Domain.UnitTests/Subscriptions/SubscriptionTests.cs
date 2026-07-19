using FluentAssertions;
using OfrenCollect.Domain.Subscriptions;

namespace OfrenCollect.Domain.UnitTests.Subscriptions;

public class SubscriptionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid PlanId = Guid.NewGuid();
    private static readonly DateTimeOffset NextDue = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private const string Reference = "OFREN-SUB-0001";

    private static Subscription NewSubscription() =>
        Subscription.Enrol(TenantId, CustomerId, PlanId, Reference, NextDue);

    [Fact]
    public void Enrol_StartsActive_WithReferenceAndDueDate_AndNoAccountYet()
    {
        var subscription = NewSubscription();

        subscription.TenantId.Should().Be(TenantId);
        subscription.CustomerId.Should().Be(CustomerId);
        subscription.PlanId.Should().Be(PlanId);
        subscription.ReservedAccountReference.Should().Be(Reference);
        subscription.NextDueDate.Should().Be(NextDue);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.ReservedAccountNumber.Should().BeNull();
        subscription.ReservedBankName.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Enrol_WithBlankReference_Throws(string reference)
    {
        var act = () => Subscription.Enrol(TenantId, CustomerId, PlanId, reference, NextDue);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AttachReservedAccount_StoresNumberAndBank()
    {
        var subscription = NewSubscription();

        subscription.AttachReservedAccount("7080124933", "Wema Bank");

        subscription.ReservedAccountNumber.Should().Be("7080124933");
        subscription.ReservedBankName.Should().Be("Wema Bank");
    }

    [Theory]
    [InlineData("", "Wema Bank")]
    [InlineData("7080124933", "")]
    public void AttachReservedAccount_WithBlankValues_Throws(string number, string bank)
    {
        var subscription = NewSubscription();

        var act = () => subscription.AttachReservedAccount(number, bank);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_SetsStatusCancelled()
    {
        var subscription = NewSubscription();

        subscription.Cancel();

        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void MarkOverdue_WhenActive_SetsOverdue()
    {
        var subscription = NewSubscription();

        subscription.MarkOverdue();

        subscription.Status.Should().Be(SubscriptionStatus.Overdue);
    }

    [Fact]
    public void MarkOverdue_WhenCancelled_LeavesCancelled()
    {
        var subscription = NewSubscription();
        subscription.Cancel();

        subscription.MarkOverdue();

        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
    }
}
