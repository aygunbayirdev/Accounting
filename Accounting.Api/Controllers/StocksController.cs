using Accounting.Application.Common.Abstractions;
using Accounting.Application.Common.Models;
using Accounting.Application.Stocks.Queries.Dto;
using Accounting.Application.Stocks.Queries.GetById;
using Accounting.Application.Stocks.Queries.List;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;
using Accounting.Domain.Constants;

namespace Accounting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StocksController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = Permissions.Stock.Read)]
    [ProducesResponseType(typeof(PagedResult<StockListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] ListStocksQuery q, CancellationToken ct)
    {
        var res = await _mediator.Send(q, ct);
        return Ok(res);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.Stock.Read)]
    [ProducesResponseType(typeof(StockDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var res = await _mediator.Send(new GetStockByIdQuery(id), ct);
        return Ok(res);
    }

    [HttpGet("export")]
    [Authorize(Policy = Permissions.Stock.Read)] // Export requires Read permission
    public async Task<IActionResult> Export(
        [FromServices] IExcelService excelService,
        [FromQuery] ListStocksQuery q,
        CancellationToken ct)
    {
        // Force high limit for export
        var exportQ = q with { PageNumber = 1, PageSize = 10000 };
        var result = await _mediator.Send(exportQ, ct);
        
        var fileContent = await excelService.ExportAsync(result.Items, "Stocks");
        return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Stocks_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
    }
}
