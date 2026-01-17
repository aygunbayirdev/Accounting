using Accounting.Api.Contracts;
using Accounting.Application.Common.Models;
using Accounting.Application.Warehouses.Commands.Create;
using Accounting.Application.Warehouses.Commands.Delete;
using Accounting.Application.Warehouses.Commands.Update;
using Accounting.Application.Warehouses.Dto;
using Accounting.Application.Warehouses.Queries.GetById;
using Accounting.Application.Warehouses.Queries.List;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;
using Accounting.Domain.Constants;

namespace Accounting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class WarehousesController : ControllerBase
{
    private readonly IMediator _mediator;

    public WarehousesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.Warehouse.Read)]
    [ProducesResponseType(typeof(PagedResult<WarehouseListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<WarehouseListItemDto>>> List([FromQuery] ListWarehousesQuery q, CancellationToken ct)
    {
        var res = await _mediator.Send(q, ct);
        return Ok(res);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.Warehouse.Read)]
    [ProducesResponseType(typeof(WarehouseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseDetailDto>> GetById([FromRoute] int id, CancellationToken ct)
    {
        var res = await _mediator.Send(new GetWarehouseByIdQuery(id), ct);
        return Ok(res);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Warehouse.Create)]
    [ProducesResponseType(typeof(WarehouseDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<WarehouseDetailDto>> Create([FromBody] CreateWarehouseCommand cmd, CancellationToken ct)
    {
        var res = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id = res.Id }, res);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.Warehouse.Update)]
    [ProducesResponseType(typeof(WarehouseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WarehouseDetailDto>> Update([FromRoute] int id, [FromBody] UpdateWarehouseCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest();
        var res = await _mediator.Send(cmd, ct);
        return Ok(res);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.Warehouse.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SoftDelete([FromRoute] int id, [FromBody] RowVersionDto body, CancellationToken ct)
    {
        await _mediator.Send(new SoftDeleteWarehouseCommand(id, body.RowVersion), ct);
        return NoContent();
    }
}
