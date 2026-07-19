using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfrenCollect.Application.Customers;
using OfrenCollect.Application.Customers.RegisterCustomer;

namespace OfrenCollect.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
public sealed class CustomersController : ControllerBase
{
    private readonly ISender _mediator;

    public CustomersController(ISender mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> Register(RegisterCustomerCommand command, CancellationToken ct) =>
        Ok(await _mediator.Send(command, ct));
}
