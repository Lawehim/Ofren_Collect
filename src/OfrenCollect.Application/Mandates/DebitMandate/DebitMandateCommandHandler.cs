using FluentValidation;
using FluentValidation.Results;
using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Common;
using OfrenCollect.Domain.Mandates;

namespace OfrenCollect.Application.Mandates.DebitMandate;

public sealed class DebitMandateCommandHandler : IRequestHandler<DebitMandateCommand, MandateDebitInitiatedResult>
{
    private const string PaymentReferencePrefix = "OFREN-DBT-";
    private const string Narration = "Ofren subscription debit";

    private readonly ISubscriptionRepository _subscriptions;
    private readonly ICustomerRepository _customers;
    private readonly IMandateRepository _mandates;
    private readonly IInvoiceRepository _invoices;
    private readonly IMandateDebitRepository _debits;
    private readonly IMonnifyMandateClient _monnify;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public DebitMandateCommandHandler(
        ISubscriptionRepository subscriptions,
        ICustomerRepository customers,
        IMandateRepository mandates,
        IInvoiceRepository invoices,
        IMandateDebitRepository debits,
        IMonnifyMandateClient monnify,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        TimeProvider clock)
    {
        _subscriptions = subscriptions;
        _customers = customers;
        _mandates = mandates;
        _invoices = invoices;
        _debits = debits;
        _monnify = monnify;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<MandateDebitInitiatedResult> Handle(DebitMandateCommand command, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.RequireTenantId();

        var subscription = await _subscriptions.GetByIdAsync(command.SubscriptionId, cancellationToken)
            ?? throw new NotFoundException("Subscription not found.");
        var mandate = await _mandates.GetActiveBySubscriptionAsync(command.SubscriptionId, cancellationToken)
            ?? throw Fail("This subscription has no active direct-debit mandate.");
        var invoice = await _invoices.GetOpenInvoiceForSubscriptionAsync(command.SubscriptionId, cancellationToken)
            ?? throw Fail("This subscription has no open invoice to charge.");
        var customer = await _customers.GetByIdAsync(subscription.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        // One debit per invoice: block a second charge (manual or scheduled) of the same invoice.
        if (await _debits.HasActiveDebitForInvoiceAsync(invoice.Id, cancellationToken))
        {
            throw Fail("This invoice is already being charged.");
        }

        var amount = invoice.OutstandingBalance;

        // Deterministic reference per invoice: if a debit is initiated with Monnify but our save
        // fails, a retry reuses the same reference and Monnify dedupes it — never a double charge.
        var paymentReference = PaymentReferencePrefix + invoice.Id.ToString("N");
        var now = _clock.GetUtcNow();

        // Call Monnify before persisting: a failure saves nothing. Monnify dedupes on our payment
        // reference, so a retry cannot double-charge.
        var result = await _monnify.DebitMandateAsync(
            new MandateDebitRequest(paymentReference, mandate.MonnifyMandateCode, amount, Narration, customer.Email),
            cancellationToken);

        var debit = MandateDebit.Initiate(
            tenantId, mandate.MandateReference, invoice.Id, paymentReference, result.TransactionReference, amount, now);
        _debits.Add(debit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new MandateDebitInitiatedResult(paymentReference, result.TransactionStatus);
    }

    private static ValidationException Fail(string message) =>
        new(new[] { new ValidationFailure(nameof(DebitMandateCommand.SubscriptionId), message) });
}
