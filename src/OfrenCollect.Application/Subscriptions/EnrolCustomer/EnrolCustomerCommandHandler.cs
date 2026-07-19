using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Common;
using OfrenCollect.Domain.Invoices;
using OfrenCollect.Domain.Plans;
using OfrenCollect.Domain.Subscriptions;

namespace OfrenCollect.Application.Subscriptions.EnrolCustomer;

public sealed class EnrolCustomerCommandHandler : IRequestHandler<EnrolCustomerCommand, SubscriptionResponse>
{
    private const string AccountReferencePrefix = "OFREN-";

    private readonly ICustomerRepository _customers;
    private readonly IPlanRepository _plans;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IInvoiceRepository _invoices;
    private readonly IMonnifyClient _monnify;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public EnrolCustomerCommandHandler(
        ICustomerRepository customers,
        IPlanRepository plans,
        ISubscriptionRepository subscriptions,
        IInvoiceRepository invoices,
        IMonnifyClient monnify,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        TimeProvider clock)
    {
        _customers = customers;
        _plans = plans;
        _subscriptions = subscriptions;
        _invoices = invoices;
        _monnify = monnify;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<SubscriptionResponse> Handle(EnrolCustomerCommand command, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.RequireTenantId();

        var customer = await _customers.GetByIdAsync(command.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");
        var plan = await _plans.GetByIdAsync(command.PlanId, cancellationToken)
            ?? throw new NotFoundException("Plan not found.");

        var accountReference = AccountReferencePrefix + Guid.NewGuid().ToString("N");
        var now = _clock.GetUtcNow();
        var nextDueDate = plan.Interval.NextDueDateFrom(now);

        var subscription = Subscription.Enrol(tenantId, customer.Id, plan.Id, accountReference, nextDueDate);

        // Provision the reserved account BEFORE persisting: if Monnify fails, the whole
        // operation throws and nothing is saved — no orphaned subscription (TC-2.8).
        var account = await _monnify.CreateReservedAccountAsync(
            new CreateReservedAccountRequest(accountReference, customer.Name, customer.Email), cancellationToken);
        subscription.AttachReservedAccount(account.AccountNumber, account.BankName);

        var invoice = Invoice.Create(tenantId, subscription.Id, plan.Amount, now, nextDueDate);

        _subscriptions.Add(subscription);
        _invoices.Add(invoice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return SubscriptionResponse.From(subscription);
    }
}
