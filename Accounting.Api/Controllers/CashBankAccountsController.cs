using Accounting.Application.CashBankAccounts.Commands.Create;
using Accounting.Application.CashBankAccounts.Commands.Delete;
using Accounting.Application.CashBankAccounts.Commands.Update;
using Accounting.Application.CashBankAccounts.Queries.Dto;
using Accounting.Application.CashBankAccounts.Queries.GetById;
using Accounting.Application.CashBankAccounts.Queries.List;
using Accounting.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;
using Accounting.Domain.Constants;

namespace Accounting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CashBankAccountsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    // GET /api/cashbankaccounts
    [HttpGet]
    [Authorize(Policy = Permissions.CashBankAccount.Read)]
    [ProducesResponseType(typeof(PagedResult<CashBankAccountListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] ListCashBankAccountsQuery q, CancellationToken ct)
    {
        var res = await _mediator.Send(q, ct);
        return Ok(res);
    }

    // GET /api/cashbankaccounts/{id}
    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.CashBankAccount.Read)]
    [ProducesResponseType(typeof(CashBankAccountDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var res = await _mediator.Send(new GetCashBankAccountByIdQuery(id), ct);
        return Ok(res);
    }

    // POST /api/cashbankaccounts
    [HttpPost]
    [Authorize(Policy = Permissions.CashBankAccount.Create)]
    [ProducesResponseType(typeof(CashBankAccountDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Create([FromBody] CreateCashBankAccountCommand cmd, CancellationToken ct)
    {
        var res = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id = res.Id }, res);
    }

    // PUT /api/cashbankaccounts/{id}
    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.CashBankAccount.Update)]
    [ProducesResponseType(typeof(CashBankAccountDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update([FromRoute] int id, [FromBody] UpdateCashBankAccountCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest("Route id and payload Id must match.");
        var res = await _mediator.Send(cmd, ct);
        return Ok(res);
    }

    // DELETE /api/cashbankaccounts/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.CashBankAccount.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SoftDelete([FromRoute] int id, [FromBody] SoftDeleteCashBankAccountCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest("Route id and payload Id must match.");
        var ok = await _mediator.Send(cmd, ct);
        return ok ? NoContent() : StatusCode(500);
    }
}
