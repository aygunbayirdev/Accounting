using Accounting.Application.StockMovements.Commands.Transfer;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Accounting.Api.Controllers;

[Route("api/stock-transfers")]
[ApiController]
public class StockTransfersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<StockTransferDetailDto>> Transfer(TransferStockCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }
}
