using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfrenCollect.Application.Transactions;
using OfrenCollect.Application.Transactions.ListTransactions;

namespace OfrenCollect.Api.Controllers;

/// <summary>Lists the tenant's reconciled transactions (the source for the refund action).</summary>
[ApiController]
[Route("api/transactions")]
[Authorize]
public sealed class TransactionsController : ControllerBase
{
    private readonly ISender _mediator;

    public TransactionsController(ISender mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransactionRow>>> List(CancellationToken ct) =>
        Ok(await _mediator.Send(new ListTransactionsQuery(), ct));
}
