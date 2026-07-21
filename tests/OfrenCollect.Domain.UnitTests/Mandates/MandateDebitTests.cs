using FluentAssertions;
using OfrenCollect.Domain.Mandates;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.UnitTests.Mandates;

public class MandateDebitTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid InvoiceId = Guid.NewGuid();
    private static readonly DateTimeOffset InitiatedAt = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    private static MandateDebit Initiate() =>
        MandateDebit.Initiate(TenantId, "OFREN-MND-1", InvoiceId, "PR-1", "MNFY|TX-1", Money.Of(5000m), InitiatedAt);

    [Fact]
    public void Initiate_CreatesPendingDebit_LinkedToInvoice()
    {
        var debit = Initiate();

        debit.TenantId.Should().Be(TenantId);
        debit.InvoiceId.Should().Be(InvoiceId);
        debit.PaymentReference.Should().Be("PR-1");
        debit.TransactionReference.Should().Be("MNFY|TX-1");
        debit.Amount.Should().Be(Money.Of(5000m));
        debit.Status.Should().Be(MandateDebitStatus.Pending);
    }

    [Fact]
    public void Initiate_WithZeroAmount_Throws()
    {
        var act = () => MandateDebit.Initiate(
            TenantId, "OFREN-MND-1", InvoiceId, "PR-1", "MNFY|TX-1", Money.Zero(), InitiatedAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkPaid_FromPending_SetsPaid()
    {
        var debit = Initiate();
        var paidAt = InitiatedAt.AddMinutes(2);

        debit.MarkPaid(paidAt);

        debit.Status.Should().Be(MandateDebitStatus.Paid);
        debit.CompletedAt.Should().Be(paidAt);
    }

    [Fact]
    public void MarkPaid_IsIdempotent()
    {
        var debit = Initiate();
        var first = InitiatedAt.AddMinutes(2);
        debit.MarkPaid(first);

        debit.MarkPaid(InitiatedAt.AddMinutes(9));

        debit.CompletedAt.Should().Be(first);
    }

    [Fact]
    public void MarkFailed_WhenPaid_Throws()
    {
        var debit = Initiate();
        debit.MarkPaid(InitiatedAt.AddMinutes(2));

        var act = () => debit.MarkFailed(InitiatedAt.AddMinutes(3));

        act.Should().Throw<InvalidOperationException>();
    }
}
