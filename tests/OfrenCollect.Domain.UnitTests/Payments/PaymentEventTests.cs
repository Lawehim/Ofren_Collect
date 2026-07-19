using FluentAssertions;
using OfrenCollect.Domain.Payments;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.UnitTests.Payments;

public class PaymentEventTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid InvoiceId = Guid.NewGuid();
    private static readonly DateTimeOffset PaidAt = new(2026, 7, 17, 9, 13, 0, TimeSpan.Zero);
    private const string Reference = "MNFY-7A3C91";
    private const string AccountNumber = "7080124933";

    [Fact]
    public void RecordMatched_StampsTenantInvoiceAndDetails()
    {
        var payment = PaymentEvent.RecordMatched(
            TenantId, Reference, AccountNumber, Money.Of(25000m), PaidAt, InvoiceId);

        payment.TenantId.Should().Be(TenantId);
        payment.MatchedInvoiceId.Should().Be(InvoiceId);
        payment.MonnifyTransactionReference.Should().Be(Reference);
        payment.ReservedAccountNumber.Should().Be(AccountNumber);
        payment.Amount.Should().Be(Money.Of(25000m));
        payment.PaidAt.Should().Be(PaidAt);
        payment.IsMatched.Should().BeTrue();
    }

    [Fact]
    public void RecordUnmatched_HasNoTenantOrInvoice()
    {
        var payment = PaymentEvent.RecordUnmatched(Reference, AccountNumber, Money.Of(25000m), PaidAt);

        payment.TenantId.Should().BeNull();
        payment.MatchedInvoiceId.Should().BeNull();
        payment.IsMatched.Should().BeFalse();
    }

    [Theory]
    [InlineData("", AccountNumber)]
    [InlineData(Reference, "")]
    public void RecordUnmatched_WithBlankReferenceOrAccount_Throws(string reference, string account)
    {
        var act = () => PaymentEvent.RecordUnmatched(reference, account, Money.Of(25000m), PaidAt);

        act.Should().Throw<ArgumentException>();
    }
}
