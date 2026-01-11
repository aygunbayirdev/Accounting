using Accounting.Application.Common.Abstractions;
using Accounting.Api.Contracts;
using Accounting.Api.Contracts.Invoices;
using Accounting.Application.Invoices.Commands.Create;
using Accounting.Application.Invoices.Commands.Delete;

using Accounting.Application.Invoices.Queries.Dto;
using Accounting.Application.Invoices.Queries.GetById;
using Accounting.Application.Invoices.Queries.List;
using Accounting.Application.Services;
using Accounting.Domain.Entities;
using Accounting.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

using Microsoft.AspNetCore.Authorization;
using Accounting.Domain.Constants;

namespace Accounting.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;
    public InvoicesController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [Authorize(Policy = Permissions.Invoice.Create)]
    [ProducesResponseType(typeof(CreateInvoiceResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateInvoiceResult>> Create([FromBody] CreateInvoiceCommand command, CancellationToken ct)
    {
        var res = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = res.Id }, res);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.Invoice.Read)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var res = await _mediator.Send(new GetInvoiceByIdQuery(id), ct);
        return Ok(res);
    }

    [HttpGet]
    [Authorize(Policy = Permissions.Invoice.Read)]
    public async Task<ActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sort = "dateUtc:desc",
        [FromQuery] int? branchId = null,
        [FromQuery] int? contactId = null,
        [FromQuery] int? type = 0, // 0 Any, 1 Sales, 2 Purchase
        [FromQuery] string? dateFromUtc = null,
        [FromQuery] string? dateToUtc = null,
        CancellationToken ct = default)
    {
        var typeEnum = Enum.IsDefined(typeof(InvoiceTypeFilter), type ?? 0)
            ? (InvoiceTypeFilter)(type ?? 0)
            : InvoiceTypeFilter.Any;

        var res = await _mediator.Send(new ListInvoicesQuery(
            pageNumber, pageSize, sort, branchId,
            contactId, typeEnum, dateFromUtc, dateToUtc
        ), ct);

        return Ok(res);
    }

    // DELETE /api/invoices/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.Invoice.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SoftDelete([FromRoute] int id, [FromBody] RowVersionDto body, CancellationToken ct)
    {
        if (id <= 0) return BadRequest();
        await _mediator.Send(new SoftDeleteInvoiceCommand(id, body.RowVersion), ct);
        return NoContent();
    }

    // TAM GÜNCELLEME
    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.Invoice.Update)]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update([FromRoute] int id, [FromBody] UpdateInvoiceCommand body, CancellationToken ct)
    {
        if (id != body.Id) return BadRequest();
        var res = await _mediator.Send(body, ct);
        return Ok(res);
    }

    [HttpGet("export")]
    [Authorize(Policy = Permissions.Invoice.Read)] // Export requires read permission
    public async Task<IActionResult> Export(
        [FromServices] IExcelService excelService,
        [FromQuery] int? branchId,
        [FromQuery] int? contactId,
        [FromQuery] int? type,
        [FromQuery] string? dateFromUtc,
        [FromQuery] string? dateToUtc,
        CancellationToken ct)
    {
         var typeEnum = Enum.IsDefined(typeof(InvoiceTypeFilter), type ?? 0)
            ? (InvoiceTypeFilter)(type ?? 0)
            : InvoiceTypeFilter.Any;

        var query = new ListInvoicesQuery(
            1, 10000, "dateUtc:desc", // 10k limit
            branchId, contactId, typeEnum, 
            dateFromUtc, dateToUtc
        );
        var result = await _mediator.Send(query, ct);
        
        var fileContent = await excelService.ExportAsync(result.Items, "Invoices");
        return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Invoices_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
    }
}
