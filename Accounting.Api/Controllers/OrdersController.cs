using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Models;
using Accounting.Application.Orders.Commands.Approve;
using Accounting.Application.Orders.Commands.Cancel;
using Accounting.Application.Orders.Commands.Create;
using Accounting.Application.Orders.Commands.CreateInvoice;
using Accounting.Application.Orders.Commands.Delete;
using Accounting.Application.Orders.Commands.Update;
using Accounting.Application.Orders.Dto;
using Accounting.Application.Orders.Queries;
using Accounting.Application.Orders.Queries.GetById;
using Accounting.Application.Orders.Queries.List;
using Accounting.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Accounting.Api.Controllers;

[Route("api/orders")]
[ApiController]
public class OrdersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderListItemDto>>> GetList(
        [FromQuery] int? branchId,
        [FromQuery] int? contactId,
        [FromQuery] OrderStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new ListOrdersQuery(branchId, contactId, status, page, pageSize);
        return Ok(await mediator.Send(query, ct));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDetailDto>> GetById(int id, CancellationToken ct)
    {
        return Ok(await mediator.Send(new GetOrderByIdQuery(id), ct));
    }

    [HttpPost]
    public async Task<ActionResult<OrderDetailDto>> Create(CreateOrderCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<OrderDetailDto>> Update(int id, UpdateOrderCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("ID mismatch");
        return Ok(await mediator.Send(command, ct));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<bool>> Delete(int id, [FromQuery] string rowVersion, CancellationToken ct)
    {
        return Ok(await mediator.Send(new DeleteOrderCommand(id, rowVersion), ct));
    }

    [HttpPut("{id}/approve")]
    public async Task<ActionResult<bool>> Approve(int id, [FromQuery] string rowVersion, CancellationToken ct)
    {
        return Ok(await mediator.Send(new ApproveOrderCommand(id, Convert.FromBase64String(rowVersion ?? "")), ct));
    }

    [HttpPut("{id}/cancel")]
    public async Task<ActionResult<bool>> Cancel(int id, [FromQuery] string rowVersion, CancellationToken ct)
    {
        return Ok(await mediator.Send(new CancelOrderCommand(id, rowVersion), ct));
    }

    [HttpPost("{id}/create-invoice")]
    public async Task<ActionResult<int>> CreateInvoice(int id, CancellationToken ct)
    {
        return Ok(await mediator.Send(new CreateInvoiceFromOrderCommand(id), ct));
    }
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromServices] IExcelService excelService,
        [FromQuery] int? branchId,
        [FromQuery] int? contactId,
        [FromQuery] OrderStatus? status,
        CancellationToken ct)
    {
        var query = new ListOrdersQuery(branchId, contactId, status, 1, 10000); // 10k limit for export
        var result = await mediator.Send(query, ct);
        
        var fileContent = await excelService.ExportAsync(result.Items, "Orders");
        return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Orders_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
    }
}
