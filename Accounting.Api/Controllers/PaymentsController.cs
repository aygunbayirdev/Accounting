using Accounting.Api.Contracts;
using Accounting.Application.Payments.Commands.Create;
using Accounting.Application.Payments.Queries.Dto;
using Accounting.Application.Payments.Queries.GetById;
using Accounting.Application.Payments.Queries.List;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;
using Accounting.Domain.Constants;
using Accounting.Application.Payments.Commands.Delete;
using Accounting.Application.Payments.Commands.Update;

namespace Accounting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    public PaymentsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [Authorize(Policy = Permissions.Payment.Create)]
    [ProducesResponseType(typeof(CreatePaymentResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreatePaymentResult>> Create([FromBody] CreatePaymentCommand cmd, CancellationToken ct)
    {
        var res = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id = res.Id }, res);
    }

    // GET /api/payments/{id}
    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.Payment.Read)]
    [ProducesResponseType(typeof(PaymentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var res = await _mediator.Send(new GetPaymentByIdQuery(id), ct);
        return Ok(res);
    }

    [HttpGet]
    [Authorize(Policy = Permissions.Payment.Read)]
    public async Task<ActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sort = "dateUtc:desc",
        [FromQuery] int? accountId = null,
        [FromQuery] int? contactId = null,
        [FromQuery] int? direction = null,       // 1=In, 2=Out
        [FromQuery] string? dateFromUtc = null,  // ISO-8601
        [FromQuery] string? dateToUtc = null,    // ISO-8601
        [FromQuery] string? currency = null,
        CancellationToken ct = default)
    {
        var dirEnum = direction is null ? null
            : (PaymentDirection?)direction;

        var res = await _mediator.Send(new ListPaymentsQuery(
            pageNumber, pageSize, sort,
            accountId, contactId, dirEnum,
            dateFromUtc, dateToUtc, currency
        ), ct);

        return Ok(res);
    }

    // PUT /api/payments/{id}
    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.Payment.Update)]
    [ProducesResponseType(typeof(PaymentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update([FromRoute] int id, [FromBody] UpdatePaymentCommand body, CancellationToken ct)
    {
        if (id != body.Id) return BadRequest();
        var res = await _mediator.Send(body, ct);
        return Ok(res);
    }

    // DELETE /api/payments/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.Payment.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SoftDelete([FromRoute] int id, [FromBody] RowVersionDto body, CancellationToken ct)
    {
        if (id <= 0) return BadRequest();
        await _mediator.Send(new SoftDeletePaymentCommand(id, body.RowVersion), ct);
        return NoContent();
    }


}
