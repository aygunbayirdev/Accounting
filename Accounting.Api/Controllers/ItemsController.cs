using Accounting.Application.Items.Commands.Create;
using Accounting.Application.Items.Commands.Delete;
using Accounting.Application.Items.Commands.Update;
using Accounting.Application.Items.Queries.Dto;
using Accounting.Application.Items.Queries.GetById;
using Accounting.Application.Items.Queries.List;
using Accounting.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;
using Accounting.Domain.Constants;

namespace Accounting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ItemsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = Permissions.Item.Read)]
    [ProducesResponseType(typeof(PagedResult<ItemListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] ListItemsQuery q, CancellationToken ct)
    {
        var res = await _mediator.Send(q, ct);
        return Ok(res);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.Item.Read)]
    [ProducesResponseType(typeof(ItemDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var res = await _mediator.Send(new GetItemByIdQuery(id), ct);
        return Ok(res);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Item.Create)]
    [ProducesResponseType(typeof(ItemDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Create([FromBody] CreateItemCommand cmd, CancellationToken ct)
    {
        var res = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id = res.Id }, res);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.Item.Update)]
    [ProducesResponseType(typeof(ItemDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update([FromRoute] int id, [FromBody] UpdateItemCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest();
        var res = await _mediator.Send(cmd, ct);
        return Ok(res);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.Item.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SoftDelete([FromRoute] int id, [FromBody] SoftDeleteItemCommand body, CancellationToken ct)
    {
        if (id != body.Id) return BadRequest();
        var ok = await _mediator.Send(body, ct);
        return ok ? NoContent() : StatusCode(500);
    }
}
