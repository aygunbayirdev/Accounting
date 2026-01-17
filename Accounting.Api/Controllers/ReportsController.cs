using Accounting.Application.Common.Abstractions;
using Accounting.Application.Reports.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;
using Accounting.Domain.Constants;
using Accounting.Application.Reports.Queries.GetContactStatement;
using Accounting.Application.Reports.Queries.Dtos;
using Accounting.Application.Reports.Queries.GetStockStatus;
using Accounting.Application.Reports.Queries.GetProfitLoss;
using Accounting.Application.Reports.Queries.GetDashboardStats;

namespace Accounting.Api.Controllers;

[Route("api/reports")]
[ApiController]
public class ReportsController(IMediator mediator, IExcelService excelService) : ControllerBase
{
    [HttpGet("dashboard")]
    [Authorize(Policy = Permissions.Report.Dashboard)]
    public async Task<ActionResult<DashboardStatsDto>> GetDashboard([FromQuery] int branchId = 1, CancellationToken ct = default)
    {
        return Ok(await mediator.Send(new GetDashboardStatsQuery(branchId), ct));
    }

    [HttpGet("contact/{contactId}/statement")]
    [Authorize(Policy = Permissions.Report.ContactStatement)]
    public async Task<ActionResult<ContactStatementDto>> GetContactStatement(
        int contactId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct = default)
    {
        return Ok(await mediator.Send(new GetContactStatementQuery(contactId, dateFrom, dateTo), ct));
    }

    [HttpGet("stock-status")]
    [Authorize(Policy = Permissions.Report.StockStatus)]
    public async Task<ActionResult<List<StockStatusDto>>> GetStockStatus(CancellationToken ct)
    {
        return Ok(await mediator.Send(new GetStockStatusQuery(), ct));
    }

    [HttpGet("stock-status/export")]
    [Authorize(Policy = Permissions.Report.StockStatus)]
    public async Task<IActionResult> ExportStockStatus(CancellationToken ct)
    {
        var data = await mediator.Send(new GetStockStatusQuery(), ct);
        var fileContent = await excelService.ExportAsync(data, "StockStatus");
        return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"StockStatus_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
    }

    [HttpGet("contact/{id}/statement/export")]
    [Authorize(Policy = Permissions.Report.ContactStatement)]
    public async Task<IActionResult> ExportContactStatement(
        int id,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
    {
        var data = await mediator.Send(new GetContactStatementQuery(id, dateFrom, dateTo), ct);
        var fileContent = await excelService.ExportAsync(data.Items, "Statement");

        var safeName = string.Join("_", data.ContactName.Split(Path.GetInvalidFileNameChars()));
        return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Ekstre_{safeName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
    }

    [HttpGet("profit-loss")]
    [Authorize(Policy = Permissions.Report.ProfitLoss)]
    public async Task<ActionResult<ProfitLossDto>> GetProfitLoss(
        [FromQuery] int? branchId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
    {
        return Ok(await mediator.Send(new GetProfitLossQuery(branchId, dateFrom, dateTo), ct));
    }
}
