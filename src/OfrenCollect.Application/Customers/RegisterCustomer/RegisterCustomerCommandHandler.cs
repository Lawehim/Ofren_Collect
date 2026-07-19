using MediatR;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Domain.Customers;

namespace OfrenCollect.Application.Customers.RegisterCustomer;

public sealed class RegisterCustomerCommandHandler : IRequestHandler<RegisterCustomerCommand, CustomerResponse>
{
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;

    public RegisterCustomerCommandHandler(
        ICustomerRepository customers, IUnitOfWork unitOfWork, ITenantContext tenantContext)
    {
        _customers = customers;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
    }

    public async Task<CustomerResponse> Handle(RegisterCustomerCommand command, CancellationToken cancellationToken)
    {
        var customer = Customer.Register(_tenantContext.RequireTenantId(), command.Name, command.Email);

        _customers.Add(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CustomerResponse.From(customer);
    }
}
