using FluentAssertions;
using NSubstitute;
using OfrenCollect.Application.Abstractions;
using OfrenCollect.Application.Abstractions.Persistence;
using OfrenCollect.Application.Customers.RegisterCustomer;
using OfrenCollect.Domain.Customers;

namespace OfrenCollect.Application.UnitTests.Customers;

public class RegisterCustomerCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private RegisterCustomerCommandHandler CreateHandler() => new(_customers, _unitOfWork, _tenantContext);

    [Fact]
    public async Task Handle_RegistersCustomerForCurrentTenant_AndReturnsResponse()
    {
        _tenantContext.CurrentTenantId.Returns(TenantId);

        var response = await CreateHandler().Handle(
            new RegisterCustomerCommand("Chidi Eze", "chidi@mail.com"), CancellationToken.None);

        response.Name.Should().Be("Chidi Eze");
        response.Email.Should().Be("chidi@mail.com");
        _customers.Received(1).Add(Arg.Is<Customer>(c => c != null && c.TenantId == TenantId));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_RejectsMalformedEmailAndBlankName()
    {
        var validator = new RegisterCustomerCommandValidator();

        validator.Validate(new RegisterCustomerCommand("Chidi", "chidi@")).IsValid.Should().BeFalse();
        validator.Validate(new RegisterCustomerCommand("", "chidi@mail.com")).IsValid.Should().BeFalse();
        validator.Validate(new RegisterCustomerCommand("Chidi", "chidi@mail.com")).IsValid.Should().BeTrue();
    }
}
