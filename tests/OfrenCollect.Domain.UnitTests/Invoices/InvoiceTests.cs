using FluentAssertions;
using OfrenCollect.Domain.Invoices;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.UnitTests.Invoices;

public class InvoiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubscriptionId = Guid.NewGuid();
    private static readonly DateTimeOffset PeriodStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DueDate = new(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);

    private static Invoice NewInvoice(decimal amountDue = 5000m) =>
        Invoice.Create(TenantId, SubscriptionId, Money.Of(amountDue), PeriodStart, DueDate);

    [Fact]
    public void Create_StartsPending_WithNothingPaid()
    {
        var invoice = NewInvoice();

        invoice.Status.Should().Be(InvoiceStatus.Pending);
        invoice.AmountPaid.Should().Be(Money.Zero());
        invoice.OutstandingBalance.Should().Be(Money.Of(5000m));
        invoice.Credit.Should().Be(Money.Zero());
    }

    [Fact]
    public void Create_StampsTenantSubscriptionAndPeriod()
    {
        var invoice = NewInvoice();

        invoice.TenantId.Should().Be(TenantId);
        invoice.SubscriptionId.Should().Be(SubscriptionId);
        invoice.AmountDue.Should().Be(Money.Of(5000m));
        invoice.DueDate.Should().Be(DueDate);
        invoice.PeriodStart.Should().Be(PeriodStart);
    }

    [Fact]
    public void ApplyPayment_WhenAmountEqualsDue_MarksPaid()
    {
        var invoice = NewInvoice();

        invoice.ApplyPayment(Money.Of(5000m));

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.OutstandingBalance.Should().Be(Money.Zero());
        invoice.Credit.Should().Be(Money.Zero());
    }

    [Fact]
    public void ApplyPayment_WhenAmountBelowDue_MarksUnderpaid_AndRecordsOutstanding()
    {
        var invoice = NewInvoice();

        invoice.ApplyPayment(Money.Of(3000m));

        invoice.Status.Should().Be(InvoiceStatus.Underpaid);
        invoice.OutstandingBalance.Should().Be(Money.Of(2000m));
        invoice.Credit.Should().Be(Money.Zero());
    }

    [Fact]
    public void ApplyPayment_WhenAmountExceedsDue_MarksOverpaid_AndRecordsCredit()
    {
        var invoice = NewInvoice();

        invoice.ApplyPayment(Money.Of(6000m));

        invoice.Status.Should().Be(InvoiceStatus.Overpaid);
        invoice.Credit.Should().Be(Money.Of(1000m));
        invoice.OutstandingBalance.Should().Be(Money.Zero());
    }

    [Fact]
    public void ApplyPayment_PartialThenRemainder_AccumulatesToPaid()
    {
        var invoice = NewInvoice();

        invoice.ApplyPayment(Money.Of(3000m));
        invoice.ApplyPayment(Money.Of(2000m));

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.AmountPaid.Should().Be(Money.Of(5000m));
        invoice.OutstandingBalance.Should().Be(Money.Zero());
    }

    [Fact]
    public void ApplyPayment_TinyAmount_MarksUnderpaid_WithoutRoundingToZero()
    {
        var invoice = NewInvoice();

        invoice.ApplyPayment(Money.Of(0.01m));

        invoice.Status.Should().Be(InvoiceStatus.Underpaid);
        invoice.OutstandingBalance.Should().Be(Money.Of(4999.99m));
    }

    [Fact]
    public void ApplyPayment_WithDifferentCurrency_Throws()
    {
        var invoice = NewInvoice();

        var act = () => invoice.ApplyPayment(Money.Of(5000m, Currency.Usd));

        act.Should().Throw<InvalidOperationException>();
    }
}
