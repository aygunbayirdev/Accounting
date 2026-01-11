using Accounting.Application.Roles.Commands.Create;
using Accounting.Application.Roles.Commands.Delete;
using Accounting.Application.Roles.Commands.Update;
using Accounting.Application.Roles.Queries.GetById;
using Accounting.Application.Roles.Queries.List;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Accounting.Domain.Constants;

namespace Accounting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly IMediator _mediator;

    public RolesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.Role.Read)]
    public async Task<IActionResult> ListRoles()
    {
        var result = await _mediator.Send(new ListRolesQuery());
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = Permissions.Role.Read)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetRoleByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Role.Create)]
    public async Task<IActionResult> Create([FromBody] CreateRoleCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Permissions.Role.Update)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRoleCommand command)
    {
        if (id != command.Id)
            return BadRequest("ID mismatch");

        await _mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = Permissions.Role.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        await _mediator.Send(new DeleteRoleCommand(id));
        return NoContent();
    }
}
