using Accounting.Api.Contracts;
using Accounting.Application.Contacts.Commands.Create;
using Accounting.Application.Contacts.Commands.Delete;
using Accounting.Application.Contacts.Commands.Update;
using Accounting.Application.Contacts.Queries.Dto;
using Accounting.Application.Contacts.Queries.GetById;
using Accounting.Application.Contacts.Queries.List;
using Accounting.Domain.Entities;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Accounting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ContactsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = Accounting.Domain.Constants.Permissions.Contact.Create)]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Create([FromBody] CreateContactCommand body, CancellationToken ct)
    {
        var res = await _mediator.Send(body, ct);
        return Ok(res);
    }

    [HttpGet("{id:int}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = Accounting.Domain.Constants.Permissions.Contact.Read)]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var res = await _mediator.Send(new GetContactByIdQuery(id), ct);
        return Ok(res);
    }

    [HttpGet]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = Accounting.Domain.Constants.Permissions.Contact.Read)]
    [ProducesResponseType(typeof(Accounting.Application.Common.Models.PagedResult<ContactListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult> List(
        [FromQuery] int? branchId,
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] bool? isCustomer,
        [FromQuery] bool? isVendor,
        [FromQuery] bool? isEmployee,
        [FromQuery] bool? isRetail,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var res = await _mediator.Send(new ListContactsQuery(branchId, search, sort, isCustomer, isVendor, isEmployee, isRetail, pageNumber, pageSize), ct);
        return Ok(res);
    }

    [HttpPut("{id:int}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = Accounting.Domain.Constants.Permissions.Contact.Update)]
    [ProducesResponseType(typeof(ContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update([FromRoute] int id, [FromBody] UpdateContactCommand body, CancellationToken ct)
    {
        if (id != body.Id) return BadRequest();
        var res = await _mediator.Send(body, ct);
        return Ok(res);
    }

    [HttpDelete("{id:int}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = Accounting.Domain.Constants.Permissions.Contact.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SoftDelete([FromRoute] int id, [FromBody] RowVersionDto body, CancellationToken ct)
    {
        if (id <= 0) return BadRequest();
        await _mediator.Send(new SoftDeleteContactCommand(id, body.RowVersion), ct);
        return NoContent();
    }
}
