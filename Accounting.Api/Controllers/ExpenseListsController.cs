using Accounting.Api.Contracts;
using Accounting.Application.Common.Models;
using Accounting.Application.ExpenseLists.Commands.Create;
using Accounting.Application.ExpenseLists.Commands.Delete;
using Accounting.Application.ExpenseLists.Commands.PostToBill;
using Accounting.Application.ExpenseLists.Commands.Review;
using Accounting.Application.ExpenseLists.Commands.Update;
using Accounting.Application.ExpenseLists.Dto;
using Accounting.Application.ExpenseLists.Queries.GetById;
using Accounting.Application.ExpenseLists.Queries.List;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;
using Accounting.Domain.Constants;

namespace Accounting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ExpenseListsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExpenseListsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // GET api/ExpenseLists
    [HttpGet]
    [Authorize(Policy = Permissions.ExpenseList.Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ExpenseListDto>>> List(
        [FromQuery] ListExpenseListsQuery query,
        CancellationToken ct)
    {
        var res = await _mediator.Send(query, ct);
        return Ok(res);
    }

    // GET api/ExpenseLists/5
    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.ExpenseList.Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseListDetailDto>> GetById(
        [FromRoute] int id,
        CancellationToken ct)
    {
        if (id <= 0) return BadRequest();

        var res = await _mediator.Send(new GetExpenseListByIdQuery(id), ct);
        return Ok(res);
    }

    // POST api/ExpenseLists
    [HttpPost]
    [Authorize(Policy = Permissions.ExpenseList.Create)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExpenseListDetailDto>> Create(
        [FromBody] CreateExpenseListCommand command,
        CancellationToken ct)
    {
        var res = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = res.Id }, res);
    }

    // PUT api/ExpenseLists/5
    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.ExpenseList.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExpenseListDetailDto>> Update(
        [FromRoute] int id,
        [FromBody] UpdateExpenseListCommand command,
        CancellationToken ct)
    {
        if (id != command.Id) return BadRequest();

        var res = await _mediator.Send(command, ct);
        return Ok(res);
    }

    // POST api/ExpenseLists/5/review
    [HttpPost("{id:int}/review")]
    [Authorize(Policy = Permissions.ExpenseList.Review)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseListDto>> Review(
        [FromRoute] int id,
        CancellationToken ct)
    {
        if (id <= 0) return BadRequest();

        var res = await _mediator.Send(new ReviewExpenseListCommand(id), ct);
        return Ok(res);
    }

    // POST api/ExpenseLists/5/post-to-bill
    [HttpPost("{id:int}/post-to-bill")]
    [Authorize(Policy = Permissions.ExpenseList.PostToBill)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostExpenseListToBillResult>> PostToBill(
        [FromRoute] int id,
        [FromBody] PostExpenseListToBillCommand body,
        CancellationToken ct)
    {
        if (id != body.ExpenseListId) return BadRequest();

        var res = await _mediator.Send(body, ct);
        return Ok(res);
    }

    // DELETE api/ExpenseLists/5
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.ExpenseList.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(
        [FromRoute] int id,
        [FromBody] RowVersionDto body,
        CancellationToken ct)
    {
        if (id <= 0) return BadRequest();

        await _mediator.Send(new SoftDeleteExpenseListCommand(id, body.RowVersion), ct);
        return NoContent();
    }
}
