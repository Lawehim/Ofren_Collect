using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Common;
using OfrenCollect.Domain.Mandates;

namespace OfrenCollect.Application.Mandates.CreateMandate;

public sealed class CreateMandateCommandHandler : IRequestHandler<CreateMandateCommand, CreateMandateResult>
{
    private const string MandateReferencePrefix = "OFREN-MND-";
    private const int MandateYears = 1;

    private readonly ISubscriptionRepository _subscriptions;
    private readonly ICustomerRepository _customers;
    private readonly IPlanRepository _plans;
    private readonly IMonnifyMandateClient _monnify;
    private readonly IMandateRepository _mandates;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public CreateMandateCommandHandler(
        ISubscriptionRepository subscriptions,
        ICustomerRepository customers,
        IPlanRepository plans,
        IMonnifyMandateClient monnify,
        IMandateRepository mandates,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        TimeProvider clock)
    {
        _subscriptions = subscriptions;
        _customers = customers;
        _plans = plans;
        _monnify = monnify;
        _mandates = mandates;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<CreateMandateResult> Handle(CreateMandateCommand command, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.RequireTenantId();

        // All three reads are tenant-scoped by the global filter, so a subscription/customer/plan
        // from another tenant is simply not found.
        var subscription = await _subscriptions.GetByIdAsync(command.SubscriptionId, cancellationToken)
            ?? throw new NotFoundException("Subscription not found.");
        var customer = await _customers.GetByIdAsync(subscription.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");
        var plan = await _plans.GetByIdAsync(subscription.PlanId, cancellationToken)
            ?? throw new NotFoundException("Plan not found.");

        var now = _clock.GetUtcNow();
        var mandateReference = MandateReferencePrefix + Guid.NewGuid().ToString("N");

        // Create with Monnify BEFORE persisting: if it fails, nothing is saved (same discipline as
        // enrolment). Monnify dedupes on our mandate reference.
        var creation = await _monnify.CreateMandateAsync(
            new MandateCreationRequest(
                mandateReference,
                plan.Amount,
                plan.Name,
                customer.Name,
                customer.Email,
                command.CustomerPhoneNumber,
                command.CustomerAddress,
                command.CustomerAccountNumber,
                command.CustomerAccountBankCode,
                now,
                now.AddYears(MandateYears)),
            cancellationToken);

        var mandate = Mandate.Request(tenantId, subscription.Id, mandateReference, creation.MandateCode, now);
        _mandates.Add(mandate);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateMandateResult(mandateReference, creation.AuthorizationLink, mandate.Status.ToString());
    }
}
