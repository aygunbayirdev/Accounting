using Accounting.Application.Common.Models;
using Accounting.Application.StockMovements.Commands.Create;
using Accounting.Application.StockMovements.Queries.Dto;
using Accounting.Application.StockMovements.Queries.GetById;
using Accounting.Application.StockMovements.Queries.List;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;
using Accounting.Domain.Constants;

namespace Accounting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockMovementsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = Permissions.StockMovement.Read)]
    [ProducesResponseType(typeof(PagedResult<StockMovementListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] ListStockMovementsQuery q, CancellationToken ct)
    {
        var res = await _mediator.Send(q, ct);
        return Ok(res);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.StockMovement.Read)]
    [ProducesResponseType(typeof(StockMovementDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var res = await _mediator.Send(new GetStockMovementByIdQuery(id), ct);
        return Ok(res);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.StockMovement.Create)]
    [ProducesResponseType(typeof(StockMovementDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Create([FromBody] CreateStockMovementCommand cmd, CancellationToken ct)
    {
        var res = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id = res.Id }, res);
    }
}
